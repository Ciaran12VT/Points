using Points.Services.Sqlite.Interfaces;
using Points.ViewModels;

namespace Points.Views.Settings;

public partial class NotificationLogPage : ContentPage
{
    public NotificationLogPage(IDbService db)
    {
        InitializeComponent();
        BindingContext = new NotificationLogViewModel(db);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is NotificationLogViewModel vm)
            await vm.LoadAsync();
    }
}
