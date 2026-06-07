using Points.ViewModels.Leaderboard;

namespace Points.Views.Leaderboard;

internal static class LeaderboardPopupChrome
{
    public static Frame BuildFrame(Size size, View content)
    {
        return new Frame
        {
            Padding = 0,
            CornerRadius = 16,
            HasShadow = true,
            WidthRequest = size.Width,
            HeightRequest = size.Height,
            Content = content
        };
    }

    public static Grid CreateRoot()
    {
        return new Grid
        {
            Padding = new Thickness(14),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 12,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E1E1E")
                : Colors.White
        };
    }

    public static View BuildHeader(Action close)
    {
        close = close ?? throw new ArgumentNullException(nameof(close));

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        header.Add(new Label
        {
            Text = "Leaderboard",
            FontAttributes = FontAttributes.Bold,
            FontSize = 22,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var closeButton = new Button
        {
            Text = "x",
            WidthRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = 0,
            BackgroundColor = Colors.Black,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };

        closeButton.Clicked += (_, _) => close();
        header.Add(closeButton, 1, 0);

        return header;
    }

    public static View BuildTabs()
    {
        var tabs = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };

        var leaderboard = CreateTabButton("Leaderboard");
        leaderboard.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.SelectLeaderboardTabCommand));
        leaderboard.SetBinding(Button.BackgroundColorProperty, nameof(LeaderboardViewModel.LeaderboardTabBackground));
        leaderboard.SetBinding(Button.TextColorProperty, nameof(LeaderboardViewModel.LeaderboardTabTextColor));

        var planner = CreateTabButton("Planner");
        planner.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.SelectPlannerTabCommand));
        planner.SetBinding(Button.BackgroundColorProperty, nameof(LeaderboardViewModel.PlannerTabBackground));
        planner.SetBinding(Button.TextColorProperty, nameof(LeaderboardViewModel.PlannerTabTextColor));

        tabs.Add(leaderboard, 0, 0);
        tabs.Add(planner, 1, 0);

        return tabs;
    }

    public static Size GetPopupSize()
    {
        try
        {
            var display = DeviceDisplay.MainDisplayInfo;
            var width = display.Width / display.Density;
            var height = display.Height / display.Density;

            return new Size(
                Math.Min(760, Math.Max(320, width - 32)),
                Math.Min(620, Math.Max(420, height - 96)));
        }
        catch
        {
            return new Size(720, 560);
        }
    }

    private static Button CreateTabButton(string text)
    {
        return new Button
        {
            Text = text,
            HeightRequest = 42,
            CornerRadius = 8,
            FontAttributes = FontAttributes.Bold
        };
    }
}
