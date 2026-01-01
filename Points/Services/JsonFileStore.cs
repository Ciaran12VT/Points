using System.Collections.Concurrent;
using System.Text.Json;

namespace Points.Services
{
    internal static class JsonFileStore
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static SemaphoreSlim GetLock(string filePath)
            => _locks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

        public static string GetFilePath(string fileName)
        {
            string scopedStoragePath = FileSystem.AppDataDirectory;
            return Path.Combine(scopedStoragePath, fileName);
        }

        public static async Task<T> ReadJsonAsync<T>(string fileName, T defaultValue)
        {
            var filePath = GetFilePath(fileName);
            var gate = GetLock(filePath);
            await gate.WaitAsync();
            try
            {
                if (!File.Exists(filePath))
                    return defaultValue;

                await using var fs = new FileStream(
                    filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 8192, useAsync: true);

                var obj = await JsonSerializer.DeserializeAsync<T>(fs, _jsonOptions);
                return obj ?? defaultValue;
            }
            finally
            {
                gate.Release();
            }
        }

        public static async Task WriteJsonAtomicAsync<T>(string fileName, T data)
        {
            var filePath = GetFilePath(fileName);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var gate = GetLock(filePath);
            await gate.WaitAsync();
            try
            {
                var tmpPath = filePath + ".tmp";
                var bakPath = filePath + ".bak";

                // 1) Write to temp
                await using (var fs = new FileStream(
                    tmpPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 8192, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(fs, data, _jsonOptions);
                    await fs.FlushAsync(); // make sure bytes hit disk
                }

                // 2) Atomic-ish replace
                // Best: File.Replace (writes backup + swaps)
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Replace(tmpPath, filePath, bakPath, ignoreMetadataErrors: true);
                        return;
                    }
                    catch
                    {
                        // Some platforms/filesystems can be finicky; fall back below.
                    }
                }

                // Fallback: move temp into place
                // (Not as strong as File.Replace, but still avoids partial writes.)
                if (File.Exists(filePath))
                {
                    // best-effort backup
                    try { File.Copy(filePath, bakPath, overwrite: true); } catch { /* ignore */ }
                    File.Delete(filePath);
                }

                File.Move(tmpPath, filePath);
            }
            finally
            {
                gate.Release();
            }
        }

        public static async Task UpdateJsonAtomicAsync<T>(string fileName, T defaultValue, Func<T, T> mutator)
        {
            var filePath = GetFilePath(fileName);
            var gate = GetLock(filePath);
            await gate.WaitAsync();
            try
            {
                // Do a read-modify-write while holding the same lock
                T current;

                if (!File.Exists(filePath))
                {
                    current = defaultValue;
                }
                else
                {
                    await using var readFs = new FileStream(
                        filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                        bufferSize: 8192, useAsync: true);

                    current = (await JsonSerializer.DeserializeAsync<T>(readFs, _jsonOptions)) ?? defaultValue;
                }

                var updated = mutator(current);

                // Write out atomically (inline to avoid lock re-entry)
                var tmpPath = filePath + ".tmp";
                var bakPath = filePath + ".bak";
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                await using (var writeFs = new FileStream(
                    tmpPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 8192, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(writeFs, updated, _jsonOptions);
                    await writeFs.FlushAsync();
                }

                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Replace(tmpPath, filePath, bakPath, ignoreMetadataErrors: true);
                        return;
                    }
                    catch { /* fall back */ }
                }

                if (File.Exists(filePath))
                {
                    try { File.Copy(filePath, bakPath, overwrite: true); } catch { }
                    File.Delete(filePath);
                }

                File.Move(tmpPath, filePath);
            }
            finally
            {
                gate.Release();
            }
        }

        // ---------------------------
        // READ
        // ---------------------------

        public static async Task<List<T>> ReadListAsync<T>(string fileName)
        {
            return await ReadJsonAsync(
                fileName,
                defaultValue: new List<T>()
            );
        }

        // ---------------------------
        // WRITE (replace entire table)
        // ---------------------------

        public static async Task WriteListAsync<T>(string fileName, List<T> rows)
        {
            rows ??= new List<T>();
            await WriteJsonAtomicAsync(fileName, rows);
        }

        // ---------------------------
        // READ → MUTATE → WRITE (atomic)
        // ---------------------------

        public static async Task UpdateListAsync<T>(
            string fileName,
            Func<List<T>, List<T>> mutator)
        {
            await UpdateJsonAtomicAsync(
                fileName,
                defaultValue: new List<T>(),
                mutator: mutator
            );
        }

        // ---------------------------
        // OPTIONAL: ensure table exists
        // ---------------------------

        public static async Task EnsureListFileExistsAsync<T>(string fileName)
        {
            var path = GetFilePath(fileName);
            if (File.Exists(path))
                return;

            await WriteJsonAtomicAsync(fileName, new List<T>());
        }
    }
}
