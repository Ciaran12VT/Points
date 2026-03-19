using SQLite;
namespace Points.Services.Sqlite.Managers.Interfaces
{
    public interface ISqliteConnectionManager
    {
        SQLiteAsyncConnection Db { get; }

        Task InitializeAsync();
        Task CloseAsync();
        Task ReinitializeAsync();

        Task BackupAsync();
        Task WipeAsync();
        Task RestoreAsync(string backupFilePath);

        DateTime? GetLastBackupUtc();
    }

}