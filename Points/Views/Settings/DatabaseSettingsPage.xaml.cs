using Points.Services.Backup;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class DatabaseSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;

    public Command ExportDatabaseCommand { get; }
    public Command ImportDatabaseCommand { get; }

    public DatabaseSettingsPage(
        IDatabaseMaintenanceService databaseMaintenance,
        IDatabaseInitializationService databaseLifecycle,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
    {
        ExportDatabaseCommand = new Command(async () => await ExportDatabaseAsync());
        ImportDatabaseCommand = new Command(async () => await ImportDatabaseAsync());

        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        BindingContext = new DatabaseSettingsViewModel(
            databaseMaintenance,
            databaseLifecycle,
            clock,
            _dialogs);
    }

    private async Task ExportDatabaseAsync()
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        var selectionPage = new BackupSelectionPage(
            "Export",
            "Choose what to include in the Points backup package.",
            "Export",
            vm.GetExportableItems(),
            _navigation,
            _dialogs);

        await _navigation.PushModalAsync(selectionPage);
        var selectedKeys = await selectionPage.SelectionTask;

        if (selectedKeys == null)
            return;

        try
        {
            var savedPath = await vm.ExportDatabaseAsync(selectedKeys);

            if (!string.IsNullOrWhiteSpace(savedPath))
                await _dialogs.DisplayAlertAsync("Export Complete", $"Saved backup to:\n{savedPath}", "OK");
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Export Failed", ex.Message, "OK");
        }
    }

    private async Task ImportDatabaseAsync()
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        BackupImportPlan? importPlan = null;

#if ANDROID
        try
        {
            importPlan = await vm.PickImportFileAsync();
            await ImportSelectedItemsAsync(vm, importPlan);
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
        finally
        {
            importPlan?.Dispose();
        }
#else
        var source = await _dialogs.DisplayActionSheetAsync(
            "Import",
            "Cancel",
            null,
            "Backup .zip or database file",
            "Backup folder");

        if (string.IsNullOrWhiteSpace(source) || source == "Cancel")
            return;

        try
        {
            importPlan = source == "Backup folder"
                ? await vm.PickImportFolderAsync()
                : await vm.PickImportFileAsync();

            await ImportSelectedItemsAsync(vm, importPlan);
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
        finally
        {
            importPlan?.Dispose();
        }
#endif
    }

    private async Task ImportSelectedItemsAsync(DatabaseSettingsViewModel vm, BackupImportPlan? importPlan)
    {
        if (importPlan == null)
            return;

        var selectionPage = new BackupSelectionPage(
            "Import",
            "Choose what to restore. Selected folders will replace the existing app folders.",
            "Import",
            importPlan.Resources,
            _navigation,
            _dialogs);

        await _navigation.PushModalAsync(selectionPage);
        var selectedKeys = await selectionPage.SelectionTask;

        if (selectedKeys == null)
            return;

        var confirm = await _dialogs.DisplayAlertAsync(
            "Import",
            "Selected data will replace the current data in the app. Continue?",
            "Import",
            "Cancel");

        if (!confirm)
            return;

        await vm.ImportDatabaseAsync(importPlan, selectedKeys);
        await _dialogs.DisplayAlertAsync("Import Complete", "Selected backup items were restored.", "OK");
    }
}
