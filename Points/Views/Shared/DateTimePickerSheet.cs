using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Points.Services.Time;

namespace Points.Views.Shared;

public static class DateTimePickerSheet
{
    /// <summary>
    /// Shows a modal date+time picker, enforcing:
    /// - hard bounds: chosen must be within [min, max]
    /// - optional async validation: validateAsync returns null/empty => valid, otherwise an error message
    /// </summary>
    public static Task<DateTime?> PickAsync(
        Page page,
        DateTime initial,
        DateTime min,
        DateTime max,
        Func<DateTime, Task<string?>>? validateAsync = null,
        string title = "Edit")
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        if (min > max) throw new ArgumentException("min must be <= max");

        var tcs = new TaskCompletionSource<DateTime?>();

        // Clamp initial into bounds so the modal never opens invalid
        initial = Clamp(initial, min, max);

        var datePicker = new DatePicker
        {
            Date = initial.Date,
            HorizontalOptions = LayoutOptions.Fill
        };

        // Note: DatePicker bounds are DATE-only, so we apply full DateTime bounds in validation
        if (min != DateTime.MinValue) datePicker.MinimumDate = min.Date;
        if (max != DateTime.MaxValue) datePicker.MaximumDate = max.Date;

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
            CornerRadius = 12,
            IsEnabled = false
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

        // Debounce + stale-result protection
        CancellationTokenSource? debounceCts = null;
        int validationVersion = 0;

        void SetError(string message)
        {
            ok.IsEnabled = false;
            validationLabel.IsVisible = true;
            validationLabel.Text = message;
        }

        void ClearError()
        {
            ok.IsEnabled = true;
            validationLabel.IsVisible = false;
            validationLabel.Text = "";
        }

        async Task ValidateAndUpdateUiAsync()
        {
            var chosen = GetChosen();

            // Hard bounds first (fast)
            if (chosen < min || chosen > max)
            {
                SetError($"Pick a time between {FormatBound(min)} and {FormatBound(max)}.");
                return;
            }

            // No async validator => valid if in range
            if (validateAsync is null)
            {
                ClearError();
                return;
            }

            // Debounce rapid changes (especially TimePicker)
            debounceCts?.Cancel();
            debounceCts?.Dispose();
            debounceCts = new CancellationTokenSource();
            var token = debounceCts.Token;

            var myVersion = ++validationVersion;

            try
            {
                // small delay to avoid hammering the validator while user scrolls
                await Task.Delay(150, token);

                // If cancelled during the delay, stop
                token.ThrowIfCancellationRequested();

                var error = await validateAsync(chosen);

                // Ignore stale validation results
                if (myVersion != validationVersion) return;

                if (string.IsNullOrWhiteSpace(error))
                    ClearError();
                else
                    SetError(error);
            }
            catch (OperationCanceledException)
            {
                // ignore - user changed value again
            }
            catch (Exception ex)
            {
                // Defensive: block OK if validation itself fails
                SetError($"Validation error: {ex.Message}");
            }
        }

        // Build modal page
        var modal = new ContentPage
        {
            Title = title,
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
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Star }
                        },
                        ColumnSpacing = 12,
                        Children = { cancel, ok }
                    }
                }
            }
        };

        Grid.SetColumn(cancel, 0);
        Grid.SetColumn(ok, 1);

        // Important: only set null result if nothing else has set the TCS
        modal.Disappearing += (_, __) => tcs.TrySetResult(null);

        cancel.Clicked += async (_, __) =>
        {
            tcs.TrySetResult(null);
            await page.Navigation.PopModalAsync();
        };

        ok.Clicked += async (_, __) =>
        {
            // Re-validate once more before accepting (covers edge cases / races)
            await ValidateAndUpdateUiAsync();
            if (!ok.IsEnabled) return;

            tcs.TrySetResult(GetChosen());
            await page.Navigation.PopModalAsync();
        };

        // Validate whenever user changes date/time
        datePicker.DateSelected += (_, __) => _ = ValidateAndUpdateUiAsync();

        timePicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == TimePicker.TimeProperty.PropertyName)
                _ = ValidateAndUpdateUiAsync();
        };

        // Initial validation
        _ = ValidateAndUpdateUiAsync();

        _ = page.Navigation.PushModalAsync(new NavigationPage(modal));
        return tcs.Task;
    }

    private static DateTime Clamp(DateTime value, DateTime min, DateTime max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static string FormatBound(DateTime dt)
    {
        if (dt == DateTime.MinValue) return "the beginning of time";
        if (dt == DateTime.MaxValue) return "the end of time";
        return TimeDisplayFormatter.FormatLocal(dt, "MMM-dd HH:mm");
    }
}
