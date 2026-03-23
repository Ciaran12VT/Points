using Points.Services.Sqlite.Managers.Interfaces;
using SQLite;
using SQLitePCL;

namespace Points.Services.Sqlite
{
    /// <summary>
    /// Owns the SQLiteAsyncConnection lifecycle and ensures the database
    /// is initialized exactly once for the application.
    /// </summary>
    public sealed class SqliteConnectionManager : ISqliteConnectionManager
    {
        private readonly string _dbPath;

        private SQLiteAsyncConnection? _db;

        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private bool _initialized;

        public SqliteConnectionManager(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentException("Database path must be provided.", nameof(dbPath));

            _dbPath = dbPath;
        }

        /// <summary>
        /// Shared SQLite connection used by repositories.
        /// </summary>
        public SQLiteAsyncConnection Db
        {
            get
            {
                if (_db == null) throw new InvalidOperationException("Database has not been initialized.");

                return _db;
            }
        }

        /// <summary>
        /// Initializes the database connection and ensures the schema is up to date.
        /// Safe to call multiple times.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            await _initSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_initialized) return;

                // Required on some platforms (Android, iOS)
                Batteries_V2.Init();

                _db = new SQLiteAsyncConnection(
                    _dbPath,
                    SQLiteOpenFlags.ReadWrite |
                    SQLiteOpenFlags.Create |
                    SQLiteOpenFlags.SharedCache);

                await _db.ExecuteAsync("PRAGMA foreign_keys = ON;").ConfigureAwait(false);

                _initialized = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// Closes the current connection.
        /// </summary>
        public async Task CloseAsync()
        {
            if (_db == null) return;

            await _db.CloseAsync().ConfigureAwait(false);

            _db = null;
            _initialized = false;
        }

        /// <summary>
        /// Deletes the database file and recreates it.
        /// </summary>
        public async Task ReinitializeAsync()
        {
            await CloseAsync().ConfigureAwait(false);

            if (File.Exists(_dbPath)) File.Delete(_dbPath);

            await InitializeAsync().ConfigureAwait(false);
        }

        public Task BackupAsync()
        {
            throw new NotImplementedException();
        }

        public Task WipeAsync()
        {
            throw new NotImplementedException();
        }

        public Task RestoreAsync(string backupFilePath)
        {
            throw new NotImplementedException();
        }

        public DateTime? GetLastBackupUtc()
        {
            throw new NotImplementedException();
        }
    }
}