using CommunityToolkit.Maui.Views;
using Points.Services.Calculations;

namespace Points.Views.Budgets;

public sealed class BudgetAmountCalculatorPopup : Popup
{
    private readonly Entry _equationEntry;
    private readonly Label _resultLabel;
    private readonly Button _okButton;
    private double _result;

    public BudgetAmountCalculatorPopup(string? initialEquation = null)
    {
        CanBeDismissedByTappingOutsideOfPopup = true;

        _equationEntry = new Entry
        {
            Keyboard = Keyboard.Text,
            Placeholder = "Equation",
            HorizontalOptions = LayoutOptions.Fill,
            MinimumWidthRequest = 150
        };

        _resultLabel = new Label
        {
            Text = "",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center,
            MinimumWidthRequest = 90
        };

        _okButton = new Button
        {
            Text = "OK",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 46,
            CornerRadius = 12,
            IsEnabled = false
        };

        _equationEntry.TextChanged += (_, __) => UpdateResult();
        _okButton.Clicked += (_, __) => Close(_result);

        if (!string.IsNullOrWhiteSpace(initialEquation))
            _equationEntry.Text = initialEquation;

        var equationRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        var equalsLabel = new Label
        {
            Text = "=",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };

        equationRow.Add(_equationEntry, 0, 0);
        equationRow.Add(equalsLabel, 1, 0);
        equationRow.Add(_resultLabel, 2, 0);

        var root = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                equationRow,
                _okButton
            }
        };

        Content = new Frame
        {
            CornerRadius = 16,
            Padding = 16,
            HasShadow = true,
            WidthRequest = 320,
            Content = root
        };

        UpdateResult();
    }

    private void UpdateResult()
    {
        if (ArithmeticExpressionEvaluator.TryEvaluate(_equationEntry.Text, out _result))
        {
            _resultLabel.Text = ArithmeticExpressionEvaluator.FormatResult(_result);
            _okButton.IsEnabled = true;
            return;
        }

        _result = 0;
        _resultLabel.Text = "";
        _okButton.IsEnabled = false;
    }
}
