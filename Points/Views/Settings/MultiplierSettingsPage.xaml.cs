using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Settings;

namespace Points.Views.Settings;

public partial class MultipliersSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;

    public MultipliersSettingsPage(
        ISettingsService settings,
        IHardModePenaltyService hardModePenalties,
        IUserMultiplierService userMultipliers,
        IClock clock,
        IAppNavigationService navigation)
    {
        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        BindingContext = new MultipliersSettingsViewModel(settings, hardModePenalties, userMultipliers, clock, ReturnToSettingsPageAsync);
    }

    private async Task ReturnToSettingsPageAsync()
    {
        await _navigation.PopAsync();
    }
}
