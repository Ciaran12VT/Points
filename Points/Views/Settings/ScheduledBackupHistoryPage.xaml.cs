using Points.Services.Backup;
using Points.Services.Time;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class ScheduledBackupHistoryPage : ContentPage
{
    public ScheduledBackupHistoryPage(
        IScheduledBackupLogStore logStore,
        ITimeZoneService timeZoneService)
    {
        InitializeComponent();
        BindingContext = new ScheduledBackupHistoryViewModel(logStore, timeZoneService);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ScheduledBackupHistoryViewModel vm)
            await vm.LoadAsync();
    }
}
