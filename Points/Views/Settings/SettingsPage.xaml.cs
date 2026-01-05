using Points.ViewModels;

namespace Points.Views.Settings;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(Services.IDbService _db)
    {
        InitializeComponent();
        BindingContext = new SettingsViewModel(_db);
    }


    private async void OnWipeDbClicked(object sender, EventArgs e)
    {
        if (BindingContext is not SettingsViewModel vm) return;

        var input = await Shell.Current.DisplayPromptAsync("Wipe DB", "Are you sure you want to wipe the DB? To proceed, type exactly \"Wipe db\".", "Wipe", "Cancel");

        if (string.IsNullOrWhiteSpace(input))
            return;

        if(input == "Wipe db")
        {
            await vm.WipeDatabase();
        }
    }

    private async void OnExportDBClicked(object sender, EventArgs e)
    {
        if (BindingContext is not SettingsViewModel vm) return;

        await vm.ExportDatabaseAsync();
    }

    private async void OnImportDBClicked(object sender, EventArgs e)
    {
        if (BindingContext is not SettingsViewModel vm) return;

        await vm.ImportDatabaseAsync();
    }
}