using Points.ViewModels;

namespace Points.Views.Details;

public partial class MultiSelectPickerPage : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<string>> _tcs = new();

    public MultiSelectPickerPage(
        string title,
        IEnumerable<string> items,
        IEnumerable<string>? initial)
    {
        InitializeComponent();
        BindingContext = new MultiSelectPickerViewModel(title, items, initial, _tcs);
    }

    public Task<IReadOnlyList<string>> Result => _tcs.Task;

    private async void OnOkClicked(object sender, EventArgs e)
    {
        if (BindingContext is MultiSelectPickerViewModel vm)
            _tcs.TrySetResult(vm.GetSelected());

        await Shell.Current.Navigation.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Shell.Current.Navigation.PopAsync();
    }
}