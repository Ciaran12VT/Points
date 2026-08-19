using Points.ViewModels.Home;
using Points.Models;
using Xunit;

namespace Points.Tests.Home;

public sealed class HomeRefreshCoordinatorTests
{
    [Fact]
    public async Task FullRefreshBurst_IsSerializedAndCoalescedToLatestRange()
    {
        var firstStarted = NewSignal();
        var releaseFirst = NewSignal();
        var contexts = new List<HomeFullRefreshContext>();
        var concurrent = 0;
        var maximumConcurrent = 0;

        await using var subject = new HomeRefreshCoordinator(
            async (context, _) =>
            {
                var active = Interlocked.Increment(ref concurrent);
                maximumConcurrent = Math.Max(maximumConcurrent, active);
                lock (contexts)
                    contexts.Add(context);

                if (context.Version == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task;
                }

                Interlocked.Decrement(ref concurrent);
            },
            (_, _, _, _) => Task.CompletedTask);

        var first = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.Initial,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 2));
        await firstStarted.Task;

        var second = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.ExternalCardData,
            new DateTime(2026, 2, 1),
            new DateTime(2026, 2, 2));
        var third = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.DateRangeChanged,
            new DateTime(2026, 3, 1),
            new DateTime(2026, 3, 2));

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.False(third.IsCompleted);
        releaseFirst.TrySetResult();

        await Task.WhenAll(first, second, third);

        Assert.Equal(1, maximumConcurrent);
        Assert.Equal(2, contexts.Count);
        Assert.Equal(new DateTime(2026, 3, 1), contexts[1].RangeStart);
        Assert.Equal(new DateTime(2026, 3, 2), contexts[1].RangeEnd);
        Assert.True(contexts[1].Reasons.HasFlag(HomeFullRefreshReason.Initial));
        Assert.True(contexts[1].Reasons.HasFlag(HomeFullRefreshReason.ExternalCardData));
        Assert.True(contexts[1].Reasons.HasFlag(HomeFullRefreshReason.DateRangeChanged));
    }

    [Fact]
    public async Task SupersededFullRefresh_CannotCommitItsSnapshot()
    {
        var firstPrepared = NewSignal();
        var releaseFirst = NewSignal();
        var committedRanges = new List<DateTime>();

        await using var subject = new HomeRefreshCoordinator(
            async (context, _) =>
            {
                if (context.Version == 1)
                {
                    firstPrepared.TrySetResult();
                    await releaseFirst.Task;
                }

                context.TryCommit(() =>
                {
                    lock (committedRanges)
                        committedRanges.Add(context.RangeStart);
                });
            },
            (_, _, _, _) => Task.CompletedTask);

        var first = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.Initial,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 2));
        await firstPrepared.Task;
        var latest = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.DateRangeChanged,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 2));

        releaseFirst.TrySetResult();
        await Task.WhenAll(first, latest);

        Assert.Equal([new DateTime(2026, 4, 1)], committedRanges);
    }

    [Fact]
    public async Task ActiveRefreshRequestedDuringFullRefresh_RunsAfterFull()
    {
        var fullStarted = NewSignal();
        var releaseFull = NewSignal();
        var sequence = new List<string>();

        await using var subject = new HomeRefreshCoordinator(
            async (_, _) =>
            {
                sequence.Add("full-start");
                fullStarted.TrySetResult();
                await releaseFull.Task;
                sequence.Add("full-end");
            },
            (_, _, _, _) =>
            {
                sequence.Add("active");
                return Task.CompletedTask;
            });

        var full = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.Initial,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 2));
        await fullStarted.Task;
        var active = subject.RequestActiveRefreshAsync();
        releaseFull.TrySetResult();

        await Task.WhenAll(full, active);
        Assert.Equal(["full-start", "full-end", "active"], sequence);
    }

    [Fact]
    public async Task FailedPass_FaultsCoveredCallerAndLaterRequestStillSucceeds()
    {
        var attempts = 0;
        await using var subject = new HomeRefreshCoordinator(
            (_, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new InvalidOperationException("Expected failure");

                return Task.CompletedTask;
            },
            (_, _, _, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.RequestFullRefreshAsync(
                HomeFullRefreshReason.Initial,
                new DateTime(2026, 1, 1),
                new DateTime(2026, 1, 2)));

        await subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.Explicit,
            new DateTime(2026, 2, 1),
            new DateTime(2026, 2, 2));

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ActiveBurst_PreservesToggleResultsInOrderAndRetainsDatabaseReadRequest()
    {
        var blockerStarted = NewSignal();
        var releaseBlocker = NewSignal();
        IReadOnlyList<ToggleActivityModelResult>? received = null;
        var requiresDatabaseRead = false;
        var firstResult = new ToggleActivityModelResult();
        var secondResult = new ToggleActivityModelResult();

        await using var subject = new HomeRefreshCoordinator(
            async (_, _) =>
            {
                blockerStarted.TrySetResult();
                await releaseBlocker.Task;
            },
            (_, results, readDatabase, _) =>
            {
                received = results;
                requiresDatabaseRead = readDatabase;
                return Task.CompletedTask;
            });

        var full = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.Initial,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 2));
        await blockerStarted.Task;

        var first = subject.RequestActiveRefreshAsync(firstResult);
        var second = subject.RequestActiveRefreshAsync(secondResult);
        var verifyDatabase = subject.RequestActiveRefreshAsync();
        releaseBlocker.TrySetResult();

        await Task.WhenAll(full, first, second, verifyDatabase);

        Assert.Equal([firstResult, secondResult], received);
        Assert.True(requiresDatabaseRead);
    }

    [Fact]
    public async Task SupersededFailure_IsCoveredByNewerSuccessfulFullRefresh()
    {
        var firstStarted = NewSignal();
        var releaseFirst = NewSignal();

        await using var subject = new HomeRefreshCoordinator(
            async (context, _) =>
            {
                if (context.Version != 1)
                    return;

                firstStarted.TrySetResult();
                await releaseFirst.Task;
                throw new InvalidOperationException("Superseded failure");
            },
            (_, _, _, _) => Task.CompletedTask);

        var first = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.Initial,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 2));
        await firstStarted.Task;
        var latest = subject.RequestFullRefreshAsync(
            HomeFullRefreshReason.DateRangeChanged,
            new DateTime(2026, 2, 1),
            new DateTime(2026, 2, 2));

        releaseFirst.TrySetResult();
        await Task.WhenAll(first, latest);
    }

    [Fact]
    public async Task ActivePayloads_ArrivingAcrossRunningPassesAreAppliedInOrder()
    {
        var firstStarted = NewSignal();
        var releaseFirst = NewSignal();
        var appliedBatches = new List<IReadOnlyList<ToggleActivityModelResult>>();
        var firstResult = new ToggleActivityModelResult();
        var secondResult = new ToggleActivityModelResult();

        await using var subject = new HomeRefreshCoordinator(
            (_, _) => Task.CompletedTask,
            async (context, results, _, _) =>
            {
                if (context.Version == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task;
                }

                if (context.IsCurrent)
                    appliedBatches.Add(results);
            });

        var first = subject.RequestActiveRefreshAsync(firstResult);
        await firstStarted.Task;
        var latest = subject.RequestActiveRefreshAsync(secondResult);
        releaseFirst.TrySetResult();

        await Task.WhenAll(first, latest);

        Assert.Collection(
            appliedBatches,
            batch => Assert.Equal([firstResult], batch),
            batch => Assert.Equal([secondResult], batch));
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
