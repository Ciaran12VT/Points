using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.ViewModels.Shortcuts;
using Points.Views.Shortcuts;

namespace Points.ViewModels.Home
{
    internal sealed class HomeDashboardShortcutWorkflowCoordinator
    {
        private readonly IShortcutService _shortcuts;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly IReadOnlyList<HomePageModel> _pages;
        private long _reloadVersion;

        public HomeDashboardShortcutWorkflowCoordinator(
            IShortcutService shortcuts,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            IReadOnlyList<HomePageModel> pages)
        {
            _shortcuts = shortcuts;
            _navigation = navigation;
            _dialogs = dialogs;
            _pages = pages;
        }

        public Task<List<ShortcutModel>> GetDashboardShortcutsAsync()
        {
            return _shortcuts.GetDashboardShortcutsAsync();
        }

        public void RebuildDashboardCells(IEnumerable<ShortcutModel> shortcuts)
        {
            var dashboard = _pages.FirstOrDefault(p => p.IsDashboard)
                ?? _pages.FirstOrDefault(p => p.Name == "Dashboard");
            if (dashboard == null)
                return;

            Interlocked.Increment(ref _reloadVersion);
            HomeDashboardShortcutCoordinator.RebuildDashboardCells(dashboard, shortcuts);
        }

        public async Task ReloadDashboardAsync()
        {
            var reloadVersion = Interlocked.Increment(ref _reloadVersion);
            var shortcuts = await _shortcuts.GetDashboardShortcutsAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (reloadVersion != Volatile.Read(ref _reloadVersion))
                    return;

                var dashboard = _pages.FirstOrDefault(p => p.IsDashboard)
                    ?? _pages.FirstOrDefault(p => p.Name == "Dashboard");
                if (dashboard != null)
                    HomeDashboardShortcutCoordinator.RebuildDashboardCells(dashboard, shortcuts);
            });
        }

        public async Task AddDashboardShortcutAsync()
        {
            var optionsByType = HomeDashboardShortcutCoordinator.BuildShortcutOptionsByType(_pages);
            var defaultTarget = HomeDashboardShortcutCoordinator.FindDefaultTarget(optionsByType);

            if (defaultTarget == null || defaultTarget.CardId <= 0)
            {
                await _dialogs.DisplayAlertAsync(
                    "No targets",
                    "No valid cards are loaded to target. Create a card first, then add a shortcut.",
                    "OK");
                return;
            }

            var shortcuts = await _shortcuts.GetDashboardShortcutsAsync();
            var nextShortcutOrder = HomeDashboardShortcutCoordinator.GetNextShortcutOrder(shortcuts);
            var model = HomeDashboardShortcutCoordinator.CreateNewShortcut(defaultTarget, nextShortcutOrder);

            await OpenDetailsPageAsync(model, optionsByType, TargetCardType.MainQuest);
        }

        public async Task OpenShortcutDetailsAsync(ShortcutModel? shortcut)
        {
            if (shortcut is null)
                return;

            var optionsByType = HomeDashboardShortcutCoordinator.BuildShortcutOptionsByType(_pages);

            if (!HomeDashboardShortcutCoordinator.TryPrepareShortcutForEdit(
                shortcut,
                optionsByType,
                out var model,
                out var defaultType))
            {
                await _dialogs.DisplayAlertAsync(
                    "No targets",
                    "No valid cards are loaded to target. Create a card first, then edit the shortcut.",
                    "OK");
                return;
            }

            await OpenDetailsPageAsync(model, optionsByType, defaultType);
        }

        private async Task OpenDetailsPageAsync(
            ShortcutModel model,
            Dictionary<TargetCardType, List<CardOption>> optionsByType,
            TargetCardType defaultType)
        {
            var groups = await _shortcuts.GetShortcutGroupsAsync();

            await _navigation.PushAsync(
                new ShortcutDetailsPage(
                    model: model,
                    optionsByType: optionsByType,
                    existingGroups: groups,
                    onSaved: CreateSaveCallback(),
                    onDelete: CreateDeleteCallback(),
                    defaultType: defaultType,
                    navigation: _navigation,
                    dialogs: _dialogs));
        }

        private Action<ShortcutModel> CreateSaveCallback()
        {
            return saved =>
            {
                _ = Task.Run(async () => await SaveShortcutAndReloadDashboardAsync(saved));
            };
        }

        private Action<ShortcutModel> CreateDeleteCallback()
        {
            return deleted =>
            {
                _ = Task.Run(async () => await DeleteShortcutAndReloadDashboardAsync(deleted));
            };
        }

        private async Task SaveShortcutAndReloadDashboardAsync(ShortcutModel saved)
        {
            if (saved.Group == null || string.IsNullOrWhiteSpace(saved.Group.Name))
                throw new InvalidOperationException("Shortcut must have a Group with a Name before saving.");

            var persistedGroup = await _shortcuts.UpsertShortcutGroupAsync(saved.Group);

            saved.ShortcutGroupId = persistedGroup.ShortcutGroupId;
            saved.Group = persistedGroup;

            await _shortcuts.SaveShortcutAsync(saved);
            await ReloadDashboardAsync();
        }

        private async Task DeleteShortcutAndReloadDashboardAsync(ShortcutModel deleted)
        {
            if (deleted.ShortcutId > 0)
                await _shortcuts.DeleteShortcutAsync(deleted.ShortcutId);

            await ReloadDashboardAsync();
        }
    }
}
