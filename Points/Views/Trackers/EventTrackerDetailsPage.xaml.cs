using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Trackers;

namespace Points.Views.Trackers;

public partial class EventTrackerDetailsPage : ContentPage
{
    public EventTrackerDetailsPage(
        EventTrackerCardModel model,
        Action<EventTrackerCardModel> onSaved,
        Func<EventTrackerCardModel, Task> onDelete,
        Func<EventTrackerCardModel, Task<bool>> wouldArchiveOnDelete,
        Action onCancelled,
        IUdmdService udmd,
        IClock clock,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        BindingContext = new EventTrackerDetailsViewModel(
            model,
            onSaved,
            onDelete,
            wouldArchiveOnDelete,
            onCancelled,
            udmd,
            navigation,
            dialogs,
            clock);
    }
}
