using CommunityToolkit.Maui.Views;
using Points.ViewModels;

namespace Points.Views.Popups;

public sealed class LeaderboardPopup : Popup
{
    private readonly LeaderboardViewModel _viewModel;

    public LeaderboardPopup(LeaderboardViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        CanBeDismissedByTappingOutsideOfPopup = true;

        var size = GetPopupSize();

        Content = new Frame
        {
            Padding = 0,
            CornerRadius = 16,
            HasShadow = true,
            WidthRequest = size.Width,
            HeightRequest = size.Height,
            Content = BuildRoot()
        };

        MainThread.BeginInvokeOnMainThread(async () => await _viewModel.RefreshAsync());
    }

    private View BuildRoot()
    {
        var root = new Grid
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

        root.Add(BuildHeader(), 0, 0);
        root.Add(BuildTabs(), 0, 1);
        root.Add(BuildContent(), 0, 2);

        return root;
    }

    private View BuildHeader()
    {
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

        var close = new Button
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

        close.Clicked += (_, _) => Close();
        header.Add(close, 1, 0);

        return header;
    }

    private View BuildTabs()
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

    private View BuildContent()
    {
        var content = new Grid();

        var leaderboard = BuildLeaderboardContent();
        leaderboard.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.IsLeaderboardSelected));

        var planner = BuildPlannerContent();
        planner.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.IsPlannerSelected));

        content.Add(leaderboard);
        content.Add(planner);

        return content;
    }

    private View BuildLeaderboardContent()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };

        var summary = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var summaryText = new Label
        {
            FontSize = 12,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        };
        summaryText.SetBinding(Label.TextProperty, nameof(LeaderboardViewModel.SummaryText));

        var busy = new ActivityIndicator
        {
            WidthRequest = 24,
            HeightRequest = 24,
            Color = Colors.Green
        };
        busy.SetBinding(ActivityIndicator.IsVisibleProperty, nameof(LeaderboardViewModel.IsBusy));
        busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(LeaderboardViewModel.IsBusy));

        summary.Add(summaryText, 0, 0);
        summary.Add(busy, 1, 0);
        root.Add(summary, 0, 0);

        var tableLayer = new Grid();

        var tableStack = new VerticalStackLayout
        {
            Spacing = 0,
            WidthRequest = 640
        };

        tableStack.Children.Add(BuildTableHeader());

        var rows = new VerticalStackLayout
        {
            Spacing = 0
        };
        BindableLayout.SetItemsSource(rows, _viewModel.Rows);
        BindableLayout.SetItemTemplate(rows, new DataTemplate(BuildTableRow));

        tableStack.Children.Add(rows);
        tableStack.Children.Add(BuildDeadAirSeparator());
        tableStack.Children.Add(BuildDeadAirRow());

        var tableScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = tableStack
        };

        var empty = new Label
        {
            Text = "No card time or points recorded today.",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Colors.Gray
        };
        empty.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.HasNoRows));

        var error = new Label
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.Red
        };
        error.SetBinding(Label.TextProperty, nameof(LeaderboardViewModel.ErrorMessage));
        error.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.HasError));

        tableLayer.Add(tableScroll);
        tableLayer.Add(empty);
        tableLayer.Add(error);

        root.Add(tableLayer, 0, 1);

        return root;
    }

    private static View BuildDeadAirSeparator()
    {
        var separator = new BoxView
        {
            HeightRequest = 1,
            Margin = new Thickness(0, 8, 0, 0),
            BackgroundColor = Colors.Gray,
            Opacity = 0.55
        };

        separator.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.IsDeadAirVisible));
        return separator;
    }

    private static View BuildDeadAirRow()
    {
        var container = new Grid
        {
            BackgroundColor = Color.FromArgb("#202020")
        };
        container.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.IsDeadAirVisible));

        var row = BuildTableRow();
        row.SetBinding(BindableObject.BindingContextProperty, nameof(LeaderboardViewModel.DeadAirRow));

        container.Add(row);
        return container;
    }

    private View BuildTableHeader()
    {
        var grid = CreateTableGrid();
        grid.Padding = new Thickness(8, 0);
        grid.HeightRequest = 42;
        grid.BackgroundColor = Color.FromArgb("#2A2A2A");

        grid.Add(new Label
        {
            Text = "Card",
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        grid.Add(CreateSortButton(nameof(LeaderboardViewModel.HoursHeaderText), nameof(LeaderboardViewModel.SortByHoursCommand)), 1, 0);
        grid.Add(CreateSortButton(nameof(LeaderboardViewModel.PercentOfTrackedHeaderText), nameof(LeaderboardViewModel.SortByPercentOfTrackedCommand)), 2, 0);
        grid.Add(CreateSortButton(nameof(LeaderboardViewModel.PercentOfDayHeaderText), nameof(LeaderboardViewModel.SortByPercentOfDayCommand)), 3, 0);
        grid.Add(CreateSortButton(nameof(LeaderboardViewModel.PointsHeaderText), nameof(LeaderboardViewModel.SortByPointsCommand)), 4, 0);

        return grid;
    }

    private static View BuildTableRow()
    {
        var grid = CreateTableGrid();
        grid.Padding = new Thickness(8, 7);
        grid.MinimumHeightRequest = 38;

        var title = new Label
        {
            FontSize = 13,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalOptions = LayoutOptions.Center
        };
        title.SetBinding(Label.TextProperty, nameof(LeaderboardRowModel.Title));

        var hours = CreateValueLabel();
        hours.SetBinding(Label.TextProperty, nameof(LeaderboardRowModel.HoursTodayText));

        var percentTracked = CreateValueLabel();
        percentTracked.SetBinding(Label.TextProperty, nameof(LeaderboardRowModel.PercentOfTrackedTimeText));

        var percentDay = CreateValueLabel();
        percentDay.SetBinding(Label.TextProperty, nameof(LeaderboardRowModel.PercentOfDayText));

        var points = CreateValueLabel();
        points.SetBinding(Label.TextProperty, nameof(LeaderboardRowModel.PointsTodayText));
        points.SetBinding(Label.TextColorProperty, nameof(LeaderboardRowModel.PointsColor));

        grid.Add(title, 0, 0);
        grid.Add(hours, 1, 0);
        grid.Add(percentTracked, 2, 0);
        grid.Add(percentDay, 3, 0);
        grid.Add(points, 4, 0);

        return grid;
    }

    private static Label CreateValueLabel()
    {
        return new Label
        {
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private static Button CreateSortButton(string textBinding, string commandBinding)
    {
        var button = new Button
        {
            Padding = new Thickness(2, 0),
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        button.SetBinding(Button.TextProperty, textBinding);
        button.SetBinding(Button.CommandProperty, commandBinding);

        return button;
    }

    private static Grid CreateTableGrid()
    {
        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(240)),
                new ColumnDefinition(new GridLength(78)),
                new ColumnDefinition(new GridLength(92)),
                new ColumnDefinition(new GridLength(82)),
                new ColumnDefinition(new GridLength(96))
            },
            ColumnSpacing = 10
        };
    }

    private static View BuildPlannerContent()
    {
        return new Grid
        {
            Children =
            {
                new Label
                {
                    Text = "Planner",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.Gray
                }
            }
        };
    }

    private static Size GetPopupSize()
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
}
