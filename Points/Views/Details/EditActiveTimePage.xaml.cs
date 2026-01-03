using Microsoft.Maui.Graphics.Text;
using Points.Models;
using Points.Services;
using Points.ViewModels;

namespace Points.Views.Details;

public partial class EditActiveTimePage : ContentPage
{
    // Returns edited tuples to the caller
    private readonly TaskCompletionSource<List<ActivityModel>> _tcs;
    private readonly IDbService _db;

    public EditActiveTimePage(List<ActivityModel> activity, TaskCompletionSource<List<ActivityModel>> tcs, Services.IDbService db)
    {
        InitializeComponent();
        _tcs = tcs;
        _db = db;

        BindingContext = new EditActiveTimeViewModel(
            activity,
            onSave: edited =>
            {
                _tcs.TrySetResult(edited);
                _ = Navigation.PopAsync();
            },
            pickDateTime: async (current) =>
            {
                var prevandnext = await _db.GetPreviousAndNextActivePeriodDateTimes(current);

                var result = await DateTimePickerSheet.PickAsync(this, current, prevandnext);
                return result;
            });
    }
}

internal static class DateTimePickerSheet
{
    public static Task<DateTime?> PickAsync(Page page, DateTime initial, Tuple<DateTime, DateTime> prevandnext)
    {
        var tcs = new TaskCompletionSource<DateTime?>();

        var prev = prevandnext?.Item1 ?? DateTime.MinValue;
        var next = prevandnext?.Item2 ?? DateTime.MaxValue;

        // Clamp initial into bounds so we never open invalid
        initial = Clamp(initial, prev, next);

        var datePicker = new DatePicker
        {
            Date = initial.Date,
            HorizontalOptions = LayoutOptions.Fill
        };

        // Optional: restrict DATE selection (not time) when bounds are meaningful
        if (prev != DateTime.MinValue) datePicker.MinimumDate = prev.Date;
        if (next != DateTime.MaxValue) datePicker.MaximumDate = next.Date;

        var timePicker = new TimePicker
        {
            Time = initial.TimeOfDay,
            HorizontalOptions = LayoutOptions.Fill
        };

        var validationLabel = new Label
        {
            Text = "",
            TextColor = Colors.Red,
            IsVisible = false
        };

        var ok = new Button
        {
            Text = "OK",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 21f,
            HeightRequest = 48,
            CornerRadius = 12
        };

        var cancel = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Gray,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 21f,
            HeightRequest = 48,
            CornerRadius = 12
        };

        DateTime GetChosen() => datePicker.Date + timePicker.Time;

        void ValidateAndUpdateUi()
        {
            var chosen = GetChosen();
            bool inRange = chosen >= prev && chosen <= next;

            ok.IsEnabled = inRange;

            // Optional message
            if (!inRange)
            {
                validationLabel.IsVisible = true;
                validationLabel.Text = $"Pick a time between {prev:G} and {next:G}";
            }
            else
            {
                validationLabel.IsVisible = false;
                validationLabel.Text = "";
            }
        }

        // Run once initially
        ValidateAndUpdateUi();

        // Revalidate whenever user changes date/time
        datePicker.DateSelected += (_, __) => ValidateAndUpdateUi();
        timePicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == TimePicker.TimeProperty.PropertyName)
                ValidateAndUpdateUi();
        };

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
                    validationLabel,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Star }
                        },
                        Children =
                        {
                            cancel,
                            ok
                        }
                    }
                }
            }
        };

        modal.Disappearing += (_, __) => tcs.TrySetResult(null);

        cancel.Clicked += async (_, __) =>
        {
            tcs.TrySetResult(null);
            await page.Navigation.PopModalAsync();
        };

        ok.Clicked += async (_, __) =>
        {
            // Double-check before accepting
            var chosen = GetChosen();
            if (chosen < prev || chosen > next)
            {
                ValidateAndUpdateUi();
                return;
            }

            tcs.TrySetResult(chosen);
            await page.Navigation.PopModalAsync();
        };

        _ = page.Navigation.PushModalAsync(new NavigationPage(modal));
        return tcs.Task;
    }

    private static DateTime Clamp(DateTime value, DateTime min, DateTime max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
