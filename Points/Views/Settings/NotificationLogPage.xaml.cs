using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class NotificationLogPage : ContentPage
{
    public NotificationLogPage(
        INotificationLogService notificationLogs,
        IClock clock,
        ITimeZoneService timeZoneService)
    {
        InitializeComponent();
        BindingContext = new NotificationLogViewModel(
            notificationLogs,
            clock,
            timeZoneService);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is NotificationLogViewModel vm)
            await vm.LoadAsync();
    }
}
