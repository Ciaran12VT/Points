using Points.Models;
namespace Points.Services.Persistence
{
    public interface IShortcutService
    {
        Task<List<ShortcutGroupModel>> GetShortcutGroupsAsync();
        Task<List<ShortcutModel>> GetDashboardShortcutsAsync();
        Task<ShortcutGroupModel> UpsertShortcutGroupAsync(ShortcutGroupModel group);
        Task<ShortcutModel> SaveShortcutAsync(ShortcutModel shortcut);
        Task DeleteShortcutAsync(long shortcutId);
        Task DeleteShortcutGroupAsync(long shortcutGroupId);
    }



}
