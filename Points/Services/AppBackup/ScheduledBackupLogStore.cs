using Points.Global;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Points.Services.Backup
{
    public interface IScheduledBackupLogStore
    {
        Task AppendAsync(ScheduledBackupLogEntry entry, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ScheduledBackupLogEntry>> GetRecentAsync(int maxEntries, CancellationToken cancellationToken = default);
        Task PruneAsync(int maxEntries, CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }

    public sealed class JsonLinesScheduledBackupLogStore : IScheduledBackupLogStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _logPath;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public JsonLinesScheduledBackupLogStore()
            : this(AppPaths.BackupAutomationLogPath)
        {
        }

        public JsonLinesScheduledBackupLogStore(string logPath)
        {
            _logPath = string.IsNullOrWhiteSpace(logPath)
                ? throw new ArgumentException("Log path is required.", nameof(logPath))
                : logPath;
        }

        public async Task AppendAsync(ScheduledBackupLogEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            await _gate.WaitAsync(cancellationToken);
            try
            {
                var directory = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var line = JsonSerializer.Serialize(Normalize(entry), JsonOptions);
                await File.AppendAllTextAsync(_logPath, line + Environment.NewLine, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<ScheduledBackupLogEntry>> GetRecentAsync(
            int maxEntries,
            CancellationToken cancellationToken = default)
        {
            if (maxEntries <= 0)
                return Array.Empty<ScheduledBackupLogEntry>();

            await _gate.WaitAsync(cancellationToken);
            try
            {
                return ReadAllEntries()
                    .OrderByDescending(entry => entry.StartedAtUtc)
                    .Take(maxEntries)
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task PruneAsync(int maxEntries, CancellationToken cancellationToken = default)
        {
            if (maxEntries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxEntries));

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_logPath))
                    return;

                var entries = ReadAllEntries()
                    .OrderByDescending(entry => entry.StartedAtUtc)
                    .Take(maxEntries)
                    .OrderBy(entry => entry.StartedAtUtc)
                    .ToList();

                var directory = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = $"{_logPath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await using (var stream = File.Create(tempPath))
                    await using (var writer = new StreamWriter(stream))
                    {
                        foreach (var entry in entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await writer.WriteLineAsync(JsonSerializer.Serialize(entry, JsonOptions));
                        }
                    }

                    File.Move(tempPath, _logPath, overwrite: true);
                }
                finally
                {
                    TryDelete(tempPath);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(_logPath))
                    File.Delete(_logPath);
            }
            finally
            {
                _gate.Release();
            }
        }

        private List<ScheduledBackupLogEntry> ReadAllEntries()
        {
            if (!File.Exists(_logPath))
                return new List<ScheduledBackupLogEntry>();

            var entries = new List<ScheduledBackupLogEntry>();
            foreach (var line in File.ReadLines(_logPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<ScheduledBackupLogEntry>(line, JsonOptions);
                    if (entry != null)
                        entries.Add(Normalize(entry));
                }
                catch (JsonException)
                {
                    // Ignore damaged log rows; later valid rows should remain readable.
                }
                catch (NotSupportedException)
                {
                    // Ignore rows from unsupported future formats.
                }
            }

            return entries;
        }

        private static ScheduledBackupLogEntry Normalize(ScheduledBackupLogEntry entry)
        {
            if (entry.RunId == Guid.Empty)
                entry.RunId = Guid.NewGuid();

            if (entry.ResourceKeys == null)
                entry.ResourceKeys = new List<string>();

            entry.ResourceKeys = entry.ResourceKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            entry.DestinationDisplayName ??= "";
            entry.FileName ??= "";

            return entry;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort cleanup for interrupted log writes.
            }
        }
    }
}
