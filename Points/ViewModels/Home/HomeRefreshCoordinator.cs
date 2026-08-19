using Points.Models;

namespace Points.ViewModels.Home
{
    [Flags]
    internal enum HomeFullRefreshReason
    {
        None = 0,
        Initial = 1 << 0,
        DateRangeChanged = 1 << 1,
        ExternalCardData = 1 << 2,
        Explicit = 1 << 3
    }

    internal sealed class HomeFullRefreshContext
    {
        private readonly Func<long, bool> _isCurrent;
        private readonly Func<long, Action, bool> _tryCommit;

        internal HomeFullRefreshContext(
            long version,
            DateTime rangeStart,
            DateTime rangeEnd,
            HomeFullRefreshReason reasons,
            Func<long, bool> isCurrent,
            Func<long, Action, bool> tryCommit)
        {
            Version = version;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Reasons = reasons;
            _isCurrent = isCurrent;
            _tryCommit = tryCommit;
        }

        public long Version { get; }
        public DateTime RangeStart { get; }
        public DateTime RangeEnd { get; }
        public HomeFullRefreshReason Reasons { get; }

        /// <summary>
        /// Returns false after a newer full refresh is requested or the coordinator is disposed.
        /// Check this immediately before committing prepared state, including inside any UI dispatch.
        /// </summary>
        public bool IsCurrent => _isCurrent(Version);

        /// <summary>
        /// Atomically verifies that this is still the newest full refresh and, when it is,
        /// executes an already-dispatched synchronous UI commit.
        /// </summary>
        public bool TryCommit(Action commit)
        {
            ArgumentNullException.ThrowIfNull(commit);
            return _tryCommit(Version, commit);
        }
    }

    internal sealed class HomeActiveRefreshContext
    {
        private readonly Func<long, bool> _isCurrent;
        private readonly Func<long, Action, bool> _tryCommit;

        internal HomeActiveRefreshContext(
            long version,
            Func<long, bool> isCurrent,
            Func<long, Action, bool> tryCommit)
        {
            Version = version;
            _isCurrent = isCurrent;
            _tryCommit = tryCommit;
        }

        public long Version { get; }

        /// <summary>
        /// Returns false after a newer full refresh is requested, or after disposal. Newer active
        /// deltas are processed after this one by the serial runner rather than superseding it.
        /// </summary>
        public bool IsCurrent => _isCurrent(Version);

        public bool TryCommit(Action commit)
        {
            ArgumentNullException.ThrowIfNull(commit);
            return _tryCommit(Version, commit);
        }
    }

    /// <summary>
    /// Serializes home refresh work and coalesces refresh bursts without dropping callers.
    /// Full refreshes use the latest requested range and subsume older active refreshes.
    /// </summary>
    internal sealed class HomeRefreshCoordinator : IAsyncDisposable
    {
        private readonly object _stateLock = new();
        private readonly Func<HomeFullRefreshContext, CancellationToken, Task> _executeFullRefreshAsync;
        private readonly Func<HomeActiveRefreshContext, IReadOnlyList<ToggleActivityModelResult>, bool, CancellationToken, Task> _executeActiveRefreshAsync;
        private readonly CancellationTokenSource _lifetimeCts = new();
        private readonly List<RefreshWaiter> _waiters = new();

        private Task? _runnerTask;
        private long _latestVersion;
        private long _settledVersion;
        private long _latestFullVersion;

        private bool _hasPendingFullRefresh;
        private long _pendingFullVersion;
        private DateTime _pendingRangeStart;
        private DateTime _pendingRangeEnd;
        private HomeFullRefreshReason _pendingFullReasons;

        private bool _hasPendingActiveRefresh;
        private long _pendingActiveVersion;
        private readonly List<ToggleActivityModelResult> _pendingToggleResults = new();
        private bool _pendingActiveRequiresDatabaseRead;
        private bool _disposed;

        public HomeRefreshCoordinator(
            Func<HomeFullRefreshContext, CancellationToken, Task> executeFullRefreshAsync,
            Func<HomeActiveRefreshContext, IReadOnlyList<ToggleActivityModelResult>, bool, CancellationToken, Task> executeActiveRefreshAsync)
        {
            _executeFullRefreshAsync = executeFullRefreshAsync
                ?? throw new ArgumentNullException(nameof(executeFullRefreshAsync));
            _executeActiveRefreshAsync = executeActiveRefreshAsync
                ?? throw new ArgumentNullException(nameof(executeActiveRefreshAsync));
        }

        /// <summary>
        /// Requests a full refresh. Multiple queued requests are reduced to one pass using the
        /// newest range and the union of their reasons. The returned task completes only after a
        /// successful pass has covered this request.
        /// </summary>
        public Task RequestFullRefreshAsync(
            HomeFullRefreshReason reason,
            DateTime rangeStart,
            DateTime rangeEnd,
            CancellationToken cancellationToken = default)
        {
            if (reason == HomeFullRefreshReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "A refresh reason is required.");

            if (rangeEnd < rangeStart)
                throw new ArgumentOutOfRangeException(nameof(rangeEnd), rangeEnd, "Range end cannot precede range start.");

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            Task waitTask;
            lock (_stateLock)
            {
                ThrowIfDisposed();

                var version = ++_latestVersion;
                _latestFullVersion = version;
                _hasPendingFullRefresh = true;
                _pendingFullVersion = version;
                _pendingRangeStart = rangeStart;
                _pendingRangeEnd = rangeEnd;
                _pendingFullReasons |= reason;

                waitTask = AddWaiter(version);
                EnsureRunnerStarted();
            }

            return WaitWithCancellationAsync(waitTask, cancellationToken);
        }

        /// <summary>
        /// Requests a refresh of the current active-card state. Queued requests coalesce into one
        /// pass. A later full refresh subsumes any active refresh requested before it.
        /// </summary>
        public Task RequestActiveRefreshAsync(
            ToggleActivityModelResult? toggleResult = null,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            Task waitTask;
            lock (_stateLock)
            {
                ThrowIfDisposed();

                var version = ++_latestVersion;
                _hasPendingActiveRefresh = true;
                _pendingActiveVersion = version;
                if (toggleResult != null)
                    _pendingToggleResults.Add(toggleResult);
                else
                    _pendingActiveRequiresDatabaseRead = true;

                waitTask = AddWaiter(version);
                EnsureRunnerStarted();
            }

            return WaitWithCancellationAsync(waitTask, cancellationToken);
        }

        /// <summary>
        /// Waits until all refresh requests known when this method is called have settled. Requests
        /// queued afterward are not included.
        /// </summary>
        public Task WaitThroughCurrentVersionAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            Task waitTask;
            lock (_stateLock)
            {
                ThrowIfDisposed();

                var version = _latestVersion;
                if (version == 0 || version <= _settledVersion)
                    return Task.CompletedTask;

                waitTask = AddWaiter(version);
            }

            return WaitWithCancellationAsync(waitTask, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            Task? runnerTask;
            List<RefreshWaiter> waiters;

            lock (_stateLock)
            {
                if (_disposed)
                {
                    runnerTask = _runnerTask;
                    waiters = new List<RefreshWaiter>();
                }
                else
                {
                    _disposed = true;
                    _hasPendingFullRefresh = false;
                    _hasPendingActiveRefresh = false;
                    _pendingToggleResults.Clear();
                    _pendingActiveRequiresDatabaseRead = false;
                    _pendingFullReasons = HomeFullRefreshReason.None;

                    _lifetimeCts.Cancel();
                    runnerTask = _runnerTask;
                    waiters = TakeAllWaiters();
                }
            }

            foreach (var waiter in waiters)
                waiter.Completion.TrySetCanceled(_lifetimeCts.Token);

            if (runnerTask != null)
                await runnerTask.ConfigureAwait(false);

            _lifetimeCts.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                while (TryTakeNextWork(out var work))
                {
                    try
                    {
                        switch (work.Kind)
                        {
                            case RefreshKind.Full:
                                await ExecuteFullRefreshAsync(work).ConfigureAwait(false);
                                break;

                            case RefreshKind.Active:
                                await ExecuteActiveRefreshAsync(work).ConfigureAwait(false);
                                break;

                            default:
                                throw new InvalidOperationException($"Unknown home refresh kind '{work.Kind}'.");
                        }
                    }
                    catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        if (!TryCarryForwardToCoveringWork(work))
                            SettleThrough(work.Version, ex);
                    }
                }
            }
            finally
            {
                CompleteRunner();
            }
        }

        private async Task ExecuteFullRefreshAsync(RefreshWork work)
        {
            var context = new HomeFullRefreshContext(
                work.Version,
                work.RangeStart,
                work.RangeEnd,
                work.Reasons,
                IsFullRefreshCurrent,
                TryCommitFullRefresh);

            await _executeFullRefreshAsync(context, _lifetimeCts.Token).ConfigureAwait(false);

            if (context.IsCurrent)
            {
                SettleThrough(work.Version);
                return;
            }

            TryCarryForwardToCoveringWork(work);
        }

        private async Task ExecuteActiveRefreshAsync(RefreshWork work)
        {
            var context = new HomeActiveRefreshContext(
                work.Version,
                IsActiveRefreshCurrent,
                TryCommitActiveRefresh);
            await _executeActiveRefreshAsync(
                    context,
                    work.ToggleResults,
                    work.ActiveRequiresDatabaseRead,
                    _lifetimeCts.Token)
                .ConfigureAwait(false);

            if (context.IsCurrent)
            {
                SettleThrough(work.Version);
                return;
            }

            TryCarryForwardToCoveringWork(work);
        }

        private bool TryTakeNextWork(out RefreshWork work)
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    work = default;
                    return false;
                }

                if (_hasPendingFullRefresh)
                {
                    work = RefreshWork.Full(
                        _pendingFullVersion,
                        _pendingRangeStart,
                        _pendingRangeEnd,
                        _pendingFullReasons);

                    _hasPendingFullRefresh = false;
                    _pendingFullReasons = HomeFullRefreshReason.None;

                    if (_hasPendingActiveRefresh && _pendingActiveVersion <= work.Version)
                    {
                        _hasPendingActiveRefresh = false;
                        _pendingToggleResults.Clear();
                        _pendingActiveRequiresDatabaseRead = false;
                    }

                    return true;
                }

                if (_hasPendingActiveRefresh)
                {
                    work = RefreshWork.Active(
                        _pendingActiveVersion,
                        _pendingToggleResults.ToArray(),
                        _pendingActiveRequiresDatabaseRead);
                    _hasPendingActiveRefresh = false;
                    _pendingToggleResults.Clear();
                    _pendingActiveRequiresDatabaseRead = false;
                    return true;
                }

                work = default;
                return false;
            }
        }

        private bool IsFullRefreshCurrent(long version)
        {
            lock (_stateLock)
            {
                return !_disposed && version >= _latestFullVersion;
            }
        }

        private bool IsActiveRefreshCurrent(long version)
        {
            lock (_stateLock)
            {
                return !_disposed
                    && version >= _latestFullVersion;
            }
        }

        private bool TryCommitActiveRefresh(long version, Action commit)
        {
            lock (_stateLock)
            {
                if (_disposed || version < _latestFullVersion)
                    return false;

                commit();
                return true;
            }
        }

        private bool TryCommitFullRefresh(long version, Action commit)
        {
            lock (_stateLock)
            {
                if (_disposed || version < _latestFullVersion)
                    return false;

                commit();
                return true;
            }
        }

        private bool TryCarryForwardToCoveringWork(RefreshWork work)
        {
            lock (_stateLock)
            {
                if (_disposed)
                    return false;

                if (_hasPendingFullRefresh && _pendingFullVersion > work.Version)
                {
                    if (work.Kind == RefreshKind.Full)
                        _pendingFullReasons |= work.Reasons;

                    // A newer full load rereads all active state and subsumes active payloads.
                    return true;
                }

                if (work.Kind != RefreshKind.Active ||
                    !_hasPendingActiveRefresh ||
                    _pendingActiveVersion <= work.Version)
                {
                    return false;
                }

                if (work.ToggleResults.Count > 0)
                    _pendingToggleResults.InsertRange(0, work.ToggleResults);

                _pendingActiveRequiresDatabaseRead |= work.ActiveRequiresDatabaseRead;
                return true;
            }
        }

        private void EnsureRunnerStarted()
        {
            if (_runnerTask == null)
                _runnerTask = Task.Run(RunAsync);
        }

        private void CompleteRunner()
        {
            lock (_stateLock)
            {
                _runnerTask = null;

                // A request cannot normally arrive between the final dequeue and this cleanup
                // because both use the same lock. Retain this check as a defensive guarantee if
                // the loop exits through cancellation or an unexpected path.
                if (!_disposed && (_hasPendingFullRefresh || _hasPendingActiveRefresh))
                    EnsureRunnerStarted();
            }
        }

        private Task AddWaiter(long version)
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add(new RefreshWaiter(version, completion));
            return completion.Task;
        }

        private void SettleThrough(long version, Exception? error = null)
        {
            List<RefreshWaiter> completed;
            lock (_stateLock)
            {
                _settledVersion = Math.Max(_settledVersion, version);
                completed = TakeWaitersThrough(version);
            }

            foreach (var waiter in completed)
            {
                if (error == null)
                    waiter.Completion.TrySetResult();
                else
                    waiter.Completion.TrySetException(error);
            }
        }

        private List<RefreshWaiter> TakeWaitersThrough(long version)
        {
            var result = new List<RefreshWaiter>();
            for (var index = _waiters.Count - 1; index >= 0; index--)
            {
                if (_waiters[index].Version > version)
                    continue;

                result.Add(_waiters[index]);
                _waiters.RemoveAt(index);
            }

            return result;
        }

        private List<RefreshWaiter> TakeAllWaiters()
        {
            var result = _waiters.ToList();
            _waiters.Clear();
            return result;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private static Task WaitWithCancellationAsync(Task task, CancellationToken cancellationToken)
        {
            return cancellationToken.CanBeCanceled
                ? task.WaitAsync(cancellationToken)
                : task;
        }

        private enum RefreshKind
        {
            Full,
            Active
        }

        private readonly record struct RefreshWork(
            RefreshKind Kind,
            long Version,
            DateTime RangeStart,
            DateTime RangeEnd,
            HomeFullRefreshReason Reasons,
            IReadOnlyList<ToggleActivityModelResult> ToggleResults,
            bool ActiveRequiresDatabaseRead)
        {
            public static RefreshWork Full(
                long version,
                DateTime rangeStart,
                DateTime rangeEnd,
                HomeFullRefreshReason reasons)
            {
                return new RefreshWork(
                    RefreshKind.Full,
                    version,
                    rangeStart,
                    rangeEnd,
                    reasons,
                    Array.Empty<ToggleActivityModelResult>(),
                    false);
            }

            public static RefreshWork Active(
                long version,
                IReadOnlyList<ToggleActivityModelResult> toggleResults,
                bool requiresDatabaseRead)
            {
                return new RefreshWork(
                    RefreshKind.Active,
                    version,
                    default,
                    default,
                    HomeFullRefreshReason.None,
                    toggleResults,
                    requiresDatabaseRead);
            }
        }

        private sealed record RefreshWaiter(long Version, TaskCompletionSource Completion);
    }
}
