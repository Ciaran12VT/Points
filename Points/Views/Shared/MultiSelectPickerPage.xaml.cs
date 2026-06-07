using Points.Services.Navigation;
using Points.ViewModels.Shared;

namespace Points.Views.Shared;

public partial class MultiSelectPickerPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _tcs = new();

    public Command OkCommand { get; }
    public Command CancelCommand { get; }

    public MultiSelectPickerPage(
        string title,
        IEnumerable<string> items,
        IEnumerable<string>? initial,
        IAppNavigationService navigation,
        bool isReadOnly = true,
        bool isSingleTag = false)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        OkCommand = new Command(async () => await ConfirmAsync());
        CancelCommand = new Command(async () => await CancelAsync());

        InitializeComponent();
        BindingContext = new MultiSelectPickerViewModel(title, items, initial, _tcs, isReadOnly, isSingleTag);
    }

    public Task<IReadOnlyList<string>?> Result => _tcs.Task;

    private async Task ConfirmAsync()
    {
        if (BindingContext is MultiSelectPickerViewModel vm)
            _tcs.TrySetResult(vm.GetSelected());

        await _navigation.PopAsync();
    }

    private async Task CancelAsync()
    {
        _tcs.TrySetResult(null);
        await _navigation.PopAsync();
    }

    private void OnSelectedTextChanged(object sender, TextChangedEventArgs e)
    {
        if (BindingContext is not MultiSelectPickerViewModel vm)
            return;

        if (vm.IsReadOnly)
            return;

        if (vm.IsSingleTag && vm.HasMultipleValues())
        {
            vm.Clear();
        }

        vm.SetSelectedFromText(e.NewTextValue);
    }
}
