using Points.Models;
using Points.ViewModels.Leaderboard;

namespace Points.Views.Leaderboard;

internal sealed class LeaderboardContentRenderer
{
    private readonly LeaderboardViewModel _viewModel;

    public LeaderboardContentRenderer(LeaderboardViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public View Build()
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

        root.Add(BuildSummary(), 0, 0);
        root.Add(BuildTableLayer(), 0, 1);

        return root;
    }

    private static Grid BuildSummary()
    {
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

        return summary;
    }

    private Grid BuildTableLayer()
    {
        var tableLayer = new Grid();

        tableLayer.Add(BuildTableScroll());
        tableLayer.Add(BuildEmptyMessage());
        tableLayer.Add(BuildErrorMessage());

        return tableLayer;
    }

    private ScrollView BuildTableScroll()
    {
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

        return new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = tableStack
        };
    }

    private static Label BuildEmptyMessage()
    {
        var empty = new Label
        {
            Text = "No card time or points recorded today.",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Colors.Gray
        };
        empty.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.HasNoRows));

        return empty;
    }

    private static Label BuildErrorMessage()
    {
        var error = new Label
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Colors.Red
        };
        error.SetBinding(Label.TextProperty, nameof(LeaderboardViewModel.ErrorMessage));
        error.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.HasError));

        return error;
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

    private static View BuildTableHeader()
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
}
