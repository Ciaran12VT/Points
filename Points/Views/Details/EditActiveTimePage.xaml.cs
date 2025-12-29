using Points.ViewModels;

namespace Points.Views.Details;

public partial class EditActiveTimePage : ContentPage
{
    // Returns edited tuples to the caller
    private readonly TaskCompletionSource<List<Tuple<DateTime, DateTime>>> _tcs;

    public EditActiveTimePage(List<Tuple<DateTime, DateTime>> activity, TaskCompletionSource<List<Tuple<DateTime, DateTime>>> tcs)
    {
        InitializeComponent();
        _tcs = tcs;

        BindingContext = new EditActiveTimeViewModel(
            activity,
            onSave: edited =>
            {
                _tcs.TrySetResult(edited);
                _ = Navigation.PopAsync();
            },
            pickDateTime: async (current) =>
            {
                return await DateTimePickerSheet.PickAsync(this, current);
            });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // If user backs out without saving, don't leave caller hanging.
        _tcs.TrySetCanceled();
    }
}

internal static class DateTimePickerSheet
{
    public static Task<DateTime?> PickAsync(Page page, DateTime initial)
    {
        var tcs = new TaskCompletionSource<DateTime?>();

        var datePicker = new DatePicker
        {
            Date = initial.Date,
            HorizontalOptions = LayoutOptions.Fill
        };

        var timePicker = new TimePicker
        {
            Time = initial.TimeOfDay,
            HorizontalOptions = LayoutOptions.Fill
        };

        var cancel = new Button { Text = "Cancel" };
        var ok = new Button { Text = "OK" };

        var modal = new ContentPage
        {
            Title = "Edit",
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children =
                    {
                        new Label { Text = "Date", FontAttributes = FontAttributes.Bold },
                        datePicker,
                        new Label { Text = "Time", FontAttributes = FontAttributes.Bold },
                        timePicker,
                        //new Grid
                        //{
                        //    ColumnDefinitions = new ColumnDefinitionCollection
                        //    {
                        //        new ColumnDefinition { Width = GridLength.Star },
                        //        new ColumnDefinition { Width = GridLength.Star }
                        //    },
                        //    ColumnSpacing = 12,
                        //    Children =
                        //    {
                        //        { cancel, 0, 0 },
                        //        { ok, 1, 0 }
                        //    }
                        //}
                    }
            }
        };

        cancel.Clicked += async (_, __) =>
        {
            tcs.TrySetResult(null);
            await page.Navigation.PopModalAsync();
        };

        ok.Clicked += async (_, __) =>
        {
            var chosen = datePicker.Date + timePicker.Time;
            tcs.TrySetResult(chosen);
            await page.Navigation.PopModalAsync();
        };

        _ = page.Navigation.PushModalAsync(new NavigationPage(modal));
        return tcs.Task;
    }
}