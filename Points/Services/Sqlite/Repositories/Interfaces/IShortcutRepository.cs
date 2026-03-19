using Points.Models;
namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IShortcutRepository
    {
        Task<List<ShortcutGroupModel>> GetShortcutGroupsAsync();
        Task<List<ShortcutModel>> GetDashboardShortcutsAsync();
        Task<ShortcutGroupModel> UpsertShortcutGroupAsync(ShortcutGroupModel group);
        Task<ShortcutModel> SaveShortcutAsync(ShortcutModel shortcut);
        Task DeleteShortcutAsync(long shortcutId);
    }

}