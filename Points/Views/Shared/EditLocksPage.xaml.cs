using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Shared;

namespace Points.Views.Shared;

public partial class EditLocksPage : ContentPage
{
    private readonly EditLocksVm _viewModel;

    public EditLocksPage(
        long cardId,
        List<LockModel> locks,
        ILockService locksService,
        List<DependencyTaskOption> dependencyOptions,
        Action onChanged,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
    {
        InitializeComponent();

        _viewModel = new EditLocksVm(
            cardId,
            locks,
            locksService,
            dependencyOptions,
            locks,
            onChanged,
            navigation,
            dialogs,
            clock);

        BindingContext = _viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.NotifyChanged();
    }
}
