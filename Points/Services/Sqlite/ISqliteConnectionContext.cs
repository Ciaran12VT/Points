using Points.Services.Persistence;
using SQLite;

namespace Points.Services.Sqlite
{
    public interface ISqliteConnectionContext : IDatabaseInitializationService
    {
        string DatabasePath { get; }

        SQLiteAsyncConnection Db { get; }

        Task RunInTransactionAsync(Action<SQLiteConnection> action);
    }
}
