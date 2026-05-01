using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Trackers;

namespace Points.Views.Trackers;

public partial class ValueTrackerDetailsPage : ContentPage
{
    public ValueTrackerDetailsPage(
        ValueTrackerCardModel model,
        Action<ValueTrackerCardModel> onSaved,
        Func<ValueTrackerCardModel, Task> onDelete,
        Action onCancelled,
        IUdmdService udmd,
        IClock clock,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        InitializeComponent();

        BindingContext = new ValueTrackerDetailsViewModel(
            model,
            onSaved,
            onDelete,
            onCancelled,
            udmd,
            navigation,
            dialogs,
            clock);
    }
}
