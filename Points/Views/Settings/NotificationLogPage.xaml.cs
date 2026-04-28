using Points.Helpers;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using Points.ViewModels;

namespace Points.Views.Settings;

public partial class NotificationLogPage : ContentPage
{
    public NotificationLogPage(IDbService db)
    {
        InitializeComponent();
        BindingContext = new NotificationLogViewModel(
            db,
            ServiceHelper.GetService<IClock>(),
            ServiceHelper.GetService<ITimeZoneService>());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is NotificationLogViewModel vm)
            await vm.LoadAsync();
    }
}
