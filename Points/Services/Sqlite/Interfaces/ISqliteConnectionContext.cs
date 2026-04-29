using SQLite;

namespace Points.Services.Sqlite.Interfaces
{
    public interface ISqliteConnectionContext : IDatabaseInitializationService
    {
        string DatabasePath { get; }

        SQLiteAsyncConnection Db { get; }

        Task RunInTransactionAsync(Action<SQLiteConnection> action);
    }
}
