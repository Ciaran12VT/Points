using Points.Helpers;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Backup;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Settings;

public partial class DatabaseSettingsPage : ContentPage
{
    public DatabaseSettingsPage(IDbService db)
    {
        InitializeComponent();
        BindingContext = new DatabaseSettingsViewModel(db, ServiceHelper.GetService<IClock>());
    }

    private async void OnWipeDbClicked(object sender, EventArgs e)
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        var input = await Shell.Current.DisplayPromptAsync(
            "Wipe DB",
            "Are you sure you want to wipe the DB? To proceed, type exactly \"Wipe db\".",
            "Wipe",
            "Cancel");

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (input == "Wipe db")
        {
            await vm.WipeDatabase();
        }
    }

    private async void OnExportDBClicked(object sender, EventArgs e)
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        var selectionPage = new BackupSelectionPage(
            "Export",
            "Choose what to include in the Points backup package.",
            "Export",
            vm.GetExportableItems());

        await Shell.Current.Navigation.PushModalAsync(selectionPage);
        var selectedKeys = await selectionPage.SelectionTask;

        if (selectedKeys == null)
            return;

        try
        {
            var savedPath = await vm.ExportDatabaseAsync(selectedKeys);

            if (!string.IsNullOrWhiteSpace(savedPath))
                await DisplayAlert("Export Complete", $"Saved backup to:\n{savedPath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Failed", ex.Message, "OK");
        }
    }

    private async void OnImportDBClicked(object sender, EventArgs e)
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
            await DisplayAlert("Import Failed", ex.Message, "OK");
        }
        finally
        {
            importPlan?.Dispose();
        }
#else
        var source = await DisplayActionSheet(
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
            await DisplayAlert("Import Failed", ex.Message, "OK");
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
            importPlan.Resources);

        await Shell.Current.Navigation.PushModalAsync(selectionPage);
        var selectedKeys = await selectionPage.SelectionTask;

        if (selectedKeys == null)
            return;

        var confirm = await DisplayAlert(
            "Import",
            "Selected data will replace the current data in the app. Continue?",
            "Import",
            "Cancel");

        if (!confirm)
            return;

        await vm.ImportDatabaseAsync(importPlan, selectedKeys);
        await DisplayAlert("Import Complete", "Selected backup items were restored.", "OK");
    }
}
