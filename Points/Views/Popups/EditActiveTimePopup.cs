using CommunityToolkit.Maui.Views;
using Points.Services.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Views.Popups
{
    public sealed class EditActiveTimePopup : Popup
    {
        private readonly Label _activationTypeLabel;
        private readonly Label _selectedTimeLabel;
        private readonly Label _relativeTimeLabel;

        private readonly Slider _slider;

        public EditActiveTimePopup(
            string activationTypeText,
            DateTime selectedTime,
            DateTime minTime,
            DateTime maxTime)
        {
            CanBeDismissedByTappingOutsideOfPopup = true;

            var totalMinutes = Math.Max(0, (maxTime - minTime).TotalMinutes);
            var initialMinutes = Math.Clamp((selectedTime - minTime).TotalMinutes, 0, totalMinutes);

            _activationTypeLabel = new Label
            {
                Text = activationTypeText,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                VerticalOptions = LayoutOptions.Center
            };

            _selectedTimeLabel = new Label
            {
                Text = TimeDisplayFormatter.FormatLocal(selectedTime, "MMM-dd HH:mm"),
                FontSize = 16,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center
            };

            _relativeTimeLabel = new Label
            {
                Text = "",
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Colors.Gray
            };

            _relativeTimeLabel.Text = FormatRelativeTime(selectedTime);

            _slider = new Slider
            {
                Minimum = 0,
                Maximum = totalMinutes,
                Value = initialMinutes
            };

            _slider.ValueChanged += (_, __) =>
            {
                var dt = minTime.AddMinutes(_slider.Value);
                _selectedTimeLabel.Text = TimeDisplayFormatter.FormatLocal(dt, "MMM-dd HH:mm");
                _relativeTimeLabel.Text = FormatRelativeTime(dt);
            };

            var minusOne = new Button { Text = "−1m", CornerRadius = 12 };
            minusOne.Clicked += (_, __) =>
            {
                _slider.Value = Math.Max(_slider.Minimum, _slider.Value - 1);
            };

            var plusOne = new Button { Text = "+1m", CornerRadius = 12 };
            plusOne.Clicked += (_, __) =>
            {
                _slider.Value = Math.Min(_slider.Maximum, _slider.Value + 1);
            };

            var editTime = new Button { Text = "Edit Time", CornerRadius = 12 };

            var done = new Button
            {
                Text = "Done",
                BackgroundColor = Colors.Green,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 20,
                HeightRequest = 48,
                CornerRadius = 12
            };

            done.Clicked += (_, __) =>
            {
                var chosen = minTime.AddMinutes(_slider.Value);
                Close(chosen);
            };

            // -------------------------
            // Layout (clean MAUI way)
            // -------------------------

            var rootGrid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto)
                },
                RowSpacing = 14
            };

            // Row 1
            var headerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12
            };


            headerGrid.Add(_activationTypeLabel, 0, 0);
            headerGrid.Add(_selectedTimeLabel, 1, 0);
            headerGrid.Add(_relativeTimeLabel, 2, 0);


            // Row 2
            var buttonRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 10
            };

            buttonRow.Add(minusOne, 0, 0);
            buttonRow.Add(plusOne, 1, 0);
            buttonRow.Add(editTime, 2, 0);

            var sliderSection = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    _slider,
                    buttonRow
                }
            };

            // Add rows to root
            rootGrid.Add(headerGrid, 0, 0);
            rootGrid.Add(sliderSection, 0, 1);
            rootGrid.Add(done, 0, 2);

            Content = new Frame
            {
                CornerRadius = 16,
                Padding = 16,
                HasShadow = true,
                Content = rootGrid
            };
        }

        private static string FormatRelativeTime(DateTime selected)
        {
            var diff = selected - DateTime.Now;
            var minutes = diff.TotalMinutes;

            if (Math.Abs(minutes) < 60)
            {
                return $"{minutes:0.0} mins";
            }

            var hours = minutes / 60.0;
            return $"{hours:0.0} hrs";
        }

    }
}
