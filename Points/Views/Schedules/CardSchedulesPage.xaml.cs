using System.Collections.ObjectModel;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Time;
using Points.ViewModels.Schedules;

namespace Points.Views.Schedules;

public partial class CardSchedulesPage : ContentPage
{
    public CardSchedulesPage(
        long cardId,
        ObservableCollection<CardSchedule> schedules,
        Action? onChanged = null,
        IAppNavigationService? navigation = null,
        IAppDialogService? dialogs = null,
        IClock? clock = null)
    {
        InitializeComponent();

        var appNavigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        var appDialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        var appClock = clock ?? throw new ArgumentNullException(nameof(clock));

        BindingContext = new CardSchedulesViewModel(
            cardId,
            schedules,
            appNavigation,
            appDialogs,
            appClock,
            onChanged);
    }
}
