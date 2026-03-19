using SQLite;
namespace Points.Services.Sqlite.Managers.Interfaces
{
    public interface ISqliteSchemaManager
    {
        Task EnsureSchemaAsync(SQLiteAsyncConnection db);
    }

}