using CommunityToolkit.Maui.Views;
using Points.Models;
using Points.ViewModels;
using System.Globalization;

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

    private View BuildPlannerContent()
    {
        const double timeRailWidth = 56;
        const double laneWidth = 286;
        const double laneGap = 8;
        const double subLaneGap = 4;
        const double subLaneWidth = (laneWidth - subLaneGap) / 2;
        const double timelineWidth = timeRailWidth + laneWidth + laneGap + laneWidth;

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };

        var controls = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6
        };

        var previous = CreateCompactPlannerButton("<");
        previous.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.PlannerPreviousDateCommand));

        var date = new DatePicker
        {
            Format = "MMM-dd-yyyy",
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };
        date.SetBinding(DatePicker.DateProperty, new Binding(nameof(LeaderboardViewModel.PlannerSelectedDate), BindingMode.TwoWay));

        var next = CreateCompactPlannerButton(">");
        next.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.PlannerNextDateCommand));

        var today = CreateCompactPlannerButton("Today");
        today.WidthRequest = 68;
        today.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.PlannerTodayCommand));

        var zoomOut = CreateCompactPlannerButton("-");
        zoomOut.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.PlannerZoomOutCommand));

        var zoomIn = CreateCompactPlannerButton("+");
        zoomIn.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.PlannerZoomInCommand));

        var zoomReset = CreateCompactPlannerButton("Fit");
        zoomReset.SetBinding(Button.CommandProperty, nameof(LeaderboardViewModel.PlannerZoomResetCommand));

        controls.Add(previous, 0, 0);
        controls.Add(date, 1, 0);
        controls.Add(next, 2, 0);
        controls.Add(today, 3, 0);
        controls.Add(zoomOut, 4, 0);
        controls.Add(zoomIn, 5, 0);
        controls.Add(zoomReset, 6, 0);

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var summary = new Label
        {
            FontSize = 12,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        summary.SetBinding(Label.TextProperty, nameof(LeaderboardViewModel.PlannerSummaryText));

        var addTask = CreatePlannerActionButton("Add Task");
        addTask.Clicked += async (_, _) => await AddPlannerTaskAsync();

        var addEvent = CreatePlannerActionButton("Add Event");
        addEvent.Clicked += async (_, _) => await AddPlannerEventAsync();

        actions.Add(summary, 0, 0);
        actions.Add(addTask, 1, 0);
        actions.Add(addEvent, 2, 0);

        var laneHeader = new Grid
        {
            WidthRequest = timelineWidth,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(timeRailWidth)),
                new ColumnDefinition(new GridLength(subLaneWidth)),
                new ColumnDefinition(new GridLength(subLaneGap)),
                new ColumnDefinition(new GridLength(subLaneWidth)),
                new ColumnDefinition(new GridLength(laneGap)),
                new ColumnDefinition(new GridLength(subLaneWidth)),
                new ColumnDefinition(new GridLength(subLaneGap)),
                new ColumnDefinition(new GridLength(subLaneWidth))
            }
        };

        var taskHeader = CreateLaneHeaderLabel("Tasks");
        Grid.SetColumnSpan(taskHeader, 3);
        laneHeader.Add(taskHeader, 1, 0);

        var eventHeader = CreateLaneHeaderLabel("Events");
        Grid.SetColumnSpan(eventHeader, 3);
        laneHeader.Add(eventHeader, 5, 0);

        laneHeader.Add(CreateLaneSubHeaderLabel("Planned"), 1, 1);
        laneHeader.Add(CreateLaneSubHeaderLabel("Actual"), 3, 1);
        laneHeader.Add(CreateLaneSubHeaderLabel("Planned"), 5, 1);
        laneHeader.Add(CreateLaneSubHeaderLabel("Actual"), 7, 1);

        var layer = new Grid();
        var timeline = new AbsoluteLayout
        {
            WidthRequest = timelineWidth,
            HeightRequest = _viewModel.PlannerContentHeight
        };

        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = timeline
        };

        var busy = new ActivityIndicator
        {
            WidthRequest = 32,
            HeightRequest = 32,
            Color = Colors.Green,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        busy.SetBinding(ActivityIndicator.IsVisibleProperty, nameof(LeaderboardViewModel.IsPlannerBusy));
        busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(LeaderboardViewModel.IsPlannerBusy));

        var empty = new Label
        {
            Text = "No plan or activity for this date.",
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        empty.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.HasNoPlannerTimelineItems));

        var error = new Label
        {
            TextColor = Colors.Red,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        error.SetBinding(Label.TextProperty, nameof(LeaderboardViewModel.PlannerErrorMessage));
        error.SetBinding(VisualElement.IsVisibleProperty, nameof(LeaderboardViewModel.HasPlannerError));

        void RebuildTimeline()
        {
            timeline.Children.Clear();
            timeline.HeightRequest = _viewModel.PlannerContentHeight;

            AddTimelineLaneBackground(timeline, timeRailWidth, laneWidth, laneGap, subLaneWidth, subLaneGap, _viewModel.PlannerContentHeight);

            foreach (var guide in _viewModel.PlannerTimeGuides)
            {
                if (!string.IsNullOrWhiteSpace(guide.Label))
                {
                    var label = new Label
                    {
                        Text = guide.Label,
                        FontSize = 10,
                        TextColor = Colors.Gray,
                        HorizontalTextAlignment = TextAlignment.End
                    };
                    AbsoluteLayout.SetLayoutBounds(label, new Rect(0, Math.Max(0, guide.Top - 8), timeRailWidth - 6, 18));
                    timeline.Children.Add(label);
                }

                var line = new BoxView
                {
                    HeightRequest = guide.IsMajor ? 1 : 0.5,
                    BackgroundColor = guide.IsMajor ? Color.FromArgb("#5A5A5A") : Color.FromArgb("#383838"),
                    Opacity = guide.IsMajor ? 0.7 : 0.45
                };
                AbsoluteLayout.SetLayoutBounds(line, new Rect(timeRailWidth, guide.Top, timelineWidth - timeRailWidth, 1));
                timeline.Children.Add(line);
            }

            foreach (var item in _viewModel.PlannerTimelineItems)
            {
                var left = item.Lane == PlannerTimelineLane.Tasks
                    ? timeRailWidth
                    : timeRailWidth + laneWidth + laneGap;

                left += item.IsPlanned ? 0 : subLaneWidth + subLaneGap;

                var block = CreatePlannerTimelineBlock(item);
                AbsoluteLayout.SetLayoutBounds(block, new Rect(left + 3, item.Top, subLaneWidth - 6, item.Height));
                timeline.Children.Add(block);
            }
        }

        _viewModel.PlannerTimelineItems.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RebuildTimeline);
        _viewModel.PlannerTimeGuides.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RebuildTimeline);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LeaderboardViewModel.PlannerContentHeight))
                MainThread.BeginInvokeOnMainThread(RebuildTimeline);
        };

        RebuildTimeline();

        layer.Add(scroll);
        layer.Add(busy);
        layer.Add(empty);
        layer.Add(error);

        root.Add(controls, 0, 0);
        root.Add(actions, 0, 1);
        root.Add(laneHeader, 0, 2);
        root.Add(layer, 0, 3);

        return root;
    }

    private static Button CreateCompactPlannerButton(string text)
    {
        return new Button
        {
            Text = text,
            HeightRequest = 34,
            WidthRequest = 42,
            Padding = new Thickness(4, 0),
            CornerRadius = 6,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Black,
            TextColor = Colors.White
        };
    }

    private static Button CreatePlannerActionButton(string text)
    {
        return new Button
        {
            Text = text,
            HeightRequest = 36,
            Padding = new Thickness(12, 0),
            CornerRadius = 6,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Green,
            TextColor = Colors.White
        };
    }

    private static Label CreateLaneHeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Gray,
            HorizontalTextAlignment = TextAlignment.Center
        };
    }

    private static Label CreateLaneSubHeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 10,
            TextColor = Colors.Gray,
            HorizontalTextAlignment = TextAlignment.Center
        };
    }

    private static void AddTimelineLaneBackground(
        AbsoluteLayout timeline,
        double timeRailWidth,
        double laneWidth,
        double laneGap,
        double subLaneWidth,
        double subLaneGap,
        double height)
    {
        var taskLane = new BoxView
        {
            BackgroundColor = Color.FromArgb("#171717"),
            Opacity = 0.92
        };
        AbsoluteLayout.SetLayoutBounds(taskLane, new Rect(timeRailWidth, 0, laneWidth, height));
        timeline.Children.Add(taskLane);

        var eventLane = new BoxView
        {
            BackgroundColor = Color.FromArgb("#171717"),
            Opacity = 0.92
        };
        AbsoluteLayout.SetLayoutBounds(eventLane, new Rect(timeRailWidth + laneWidth + laneGap, 0, laneWidth, height));
        timeline.Children.Add(eventLane);

        AddVerticalDivider(timeline, timeRailWidth + subLaneWidth + (subLaneGap / 2), height);
        AddVerticalDivider(timeline, timeRailWidth + laneWidth + laneGap + subLaneWidth + (subLaneGap / 2), height);
    }

    private static void AddVerticalDivider(AbsoluteLayout timeline, double left, double height)
    {
        var divider = new BoxView
        {
            WidthRequest = 1,
            BackgroundColor = Color.FromArgb("#4A4A4A"),
            Opacity = 0.75
        };
        AbsoluteLayout.SetLayoutBounds(divider, new Rect(left, 0, 1, height));
        timeline.Children.Add(divider);
    }

    private View CreatePlannerTimelineBlock(PlannerTimelineItemModel item)
    {
        var title = new Label
        {
            Text = item.Title,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = item.TextColor,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var subtitle = new Label
        {
            Text = item.Subtitle,
            FontSize = 10,
            TextColor = item.TextColor,
            Opacity = 0.88,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var frame = new Frame
        {
            Padding = new Thickness(7, 4),
            CornerRadius = 6,
            HasShadow = false,
            BackgroundColor = item.BackgroundColor,
            BorderColor = item.Status == PlannerMatchStatus.Missing ? Colors.White : item.BackgroundColor,
            HeightRequest = item.Height,
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children = { title, subtitle }
            }
        };

        frame.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnPlannerTimelineItemTappedAsync(item))
        });

        return frame;
    }

    private async Task AddPlannerTaskAsync()
    {
        var task = await PromptPlannerTaskAsync(null);
        if (task == null) return;

        try
        {
            await _viewModel.UpsertPlannerTaskAsync(task);
        }
        catch (Exception ex)
        {
            await ShowPlannerAlertAsync("Task not saved", ex.Message);
        }
    }

    private async Task AddPlannerEventAsync()
    {
        var plannerEvent = await PromptPlannerEventAsync(null);
        if (plannerEvent == null) return;

        try
        {
            await _viewModel.UpsertPlannerEventAsync(plannerEvent);
        }
        catch (Exception ex)
        {
            await ShowPlannerAlertAsync("Event not saved", ex.Message);
        }
    }

    private async Task OnPlannerTimelineItemTappedAsync(PlannerTimelineItemModel item)
    {
        var page = GetHostPage();
        if (page == null)
            return;

        if (!item.IsPlanned)
        {
            await page.DisplayAlert(item.Title, item.Subtitle, "OK");
            return;
        }

        var action = await page.DisplayActionSheet(item.Title, "Cancel", "Delete", "Edit");
        if (action == "Delete")
        {
            var confirm = await page.DisplayAlert("Delete item?", item.Title, "Delete", "Cancel");
            if (!confirm) return;

            try
            {
                await _viewModel.DeletePlannerItemAsync(item);
            }
            catch (Exception ex)
            {
                await ShowPlannerAlertAsync("Item not deleted", ex.Message);
            }

            return;
        }

        if (action != "Edit")
            return;

        try
        {
            if (item.Task != null)
            {
                var edited = await PromptPlannerTaskAsync(item.Task);
                if (edited != null)
                    await _viewModel.UpsertPlannerTaskAsync(edited);
            }
            else if (item.Event != null)
            {
                var edited = await PromptPlannerEventAsync(item.Event);
                if (edited != null)
                    await _viewModel.UpsertPlannerEventAsync(edited);
            }
        }
        catch (Exception ex)
        {
            await ShowPlannerAlertAsync("Item not saved", ex.Message);
        }
    }

    private async Task<PlannerTaskModel?> PromptPlannerTaskAsync(PlannerTaskModel? existing)
    {
        var page = GetHostPage();
        if (page == null)
            return null;

        var options = _viewModel.PlannerTaskOptions.ToList();
        if (options.Count == 0)
        {
            await page.DisplayAlert("No task cards", "There are no task cards available for this date.", "OK");
            return null;
        }

        var selected = await PickTaskOptionAsync(page, options, existing?.CardId);
        if (selected == null)
            return null;

        var defaultStart = existing?.PlannedStart ?? _viewModel.PlannerSelectedDate.Date.AddHours(9);
        var start = await PromptTimeAsync(page, "Task start", defaultStart);
        if (!start.HasValue)
            return null;

        var defaultEnd = existing?.PlannedEnd ?? start.Value.AddHours(1);
        var end = await PromptTimeAsync(page, "Task end", defaultEnd);
        if (!end.HasValue)
            return null;

        return new PlannerTaskModel
        {
            PlannerTaskId = existing?.PlannerTaskId ?? 0,
            PlannerId = existing?.PlannerId ?? 0,
            CardId = selected.CardId,
            CardKind = selected.Kind,
            PlannedStart = start.Value,
            PlannedEnd = end.Value
        };
    }

    private async Task<PlannerEventModel?> PromptPlannerEventAsync(PlannerEventModel? existing)
    {
        var page = GetHostPage();
        if (page == null)
            return null;

        var kind = existing?.EventKind;
        if (!kind.HasValue)
        {
            var kindChoice = await page.DisplayActionSheet(
                "Event type",
                "Cancel",
                null,
                "SC step reps",
                "Mission complete",
                "Mission fail");

            kind = kindChoice switch
            {
                "SC step reps" => PlannerEventKind.ScStepRep,
                "Mission complete" => PlannerEventKind.MissionComplete,
                "Mission fail" => PlannerEventKind.MissionFail,
                _ => null
            };
        }

        if (!kind.HasValue)
            return null;

        var plannedTime = await PromptTimeAsync(
            page,
            "Event time",
            existing?.PlannedTime ?? _viewModel.PlannerSelectedDate.Date.AddHours(9));

        if (!plannedTime.HasValue)
            return null;

        if (kind == PlannerEventKind.ScStepRep)
        {
            var stepOptions = _viewModel.PlannerStepOptions.ToList();
            if (stepOptions.Count == 0)
            {
                await page.DisplayAlert("No SC steps", "There are no SC steps available for this date.", "OK");
                return null;
            }

            var step = await PickStepOptionAsync(page, stepOptions, existing?.ScCardStepId);
            if (step == null)
                return null;

            var count = await PromptCountAsync(page, existing?.PlannedCount ?? 1);
            if (!count.HasValue)
                return null;

            return new PlannerEventModel
            {
                PlannerEventId = existing?.PlannerEventId ?? 0,
                PlannerId = existing?.PlannerId ?? 0,
                EventKind = PlannerEventKind.ScStepRep,
                CardId = step.CardId,
                ScCardStepId = step.ScCardStepId,
                PlannedTime = plannedTime.Value,
                PlannedCount = count.Value
            };
        }

        var missionOptions = _viewModel.PlannerMissionOptions.ToList();
        if (missionOptions.Count == 0)
        {
            await page.DisplayAlert("No missions", "There are no missions available for this date.", "OK");
            return null;
        }

        var mission = await PickMissionOptionAsync(page, missionOptions, existing?.CardId);
        if (mission == null)
            return null;

        return new PlannerEventModel
        {
            PlannerEventId = existing?.PlannerEventId ?? 0,
            PlannerId = existing?.PlannerId ?? 0,
            EventKind = kind.Value,
            CardId = mission.CardId,
            ScCardStepId = null,
            PlannedTime = plannedTime.Value,
            PlannedCount = 1
        };
    }

    private async Task<PlannerTaskCardOption?> PickTaskOptionAsync(Page page, List<PlannerTaskCardOption> options, long? currentCardId)
    {
        var labels = options.Select(o => o.DisplayTitle).ToList();
        var choice = await page.DisplayActionSheet("Task card", "Cancel", null, labels.ToArray());

        return choice == null || choice == "Cancel"
            ? null
            : options.FirstOrDefault(o => o.DisplayTitle == choice);
    }

    private async Task<PlannerStepOption?> PickStepOptionAsync(Page page, List<PlannerStepOption> options, int? currentStepId)
    {
        var labels = options.Select(o => o.DisplayTitle).ToList();
        var choice = await page.DisplayActionSheet("SC step", "Cancel", null, labels.ToArray());

        return choice == null || choice == "Cancel"
            ? null
            : options.FirstOrDefault(o => o.DisplayTitle == choice);
    }

    private async Task<PlannerMissionOption?> PickMissionOptionAsync(Page page, List<PlannerMissionOption> options, long? currentCardId)
    {
        var labels = options.Select(o => o.Title).ToList();
        var choice = await page.DisplayActionSheet("Mission", "Cancel", null, labels.ToArray());

        return choice == null || choice == "Cancel"
            ? null
            : options.FirstOrDefault(o => o.Title == choice);
    }

    private async Task<DateTime?> PromptTimeAsync(Page page, string title, DateTime initial)
    {
        var input = await page.DisplayPromptAsync(
            title,
            "Enter time as HH:mm",
            accept: "OK",
            cancel: "Cancel",
            initialValue: initial.ToString("HH:mm", CultureInfo.InvariantCulture),
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (!TryParsePlannerTime(input, out var time))
        {
            await page.DisplayAlert("Invalid time", "Use HH:mm, for example 09:30.", "OK");
            return null;
        }

        return _viewModel.PlannerSelectedDate.Date.Add(time);
    }

    private async Task<int?> PromptCountAsync(Page page, int initial)
    {
        var input = await page.DisplayPromptAsync(
            "Rep count",
            "Enter planned rep count",
            accept: "OK",
            cancel: "Cancel",
            initialValue: Math.Max(1, initial).ToString(CultureInfo.InvariantCulture),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            && !int.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out count))
        {
            await page.DisplayAlert("Invalid count", "Enter a whole number greater than zero.", "OK");
            return null;
        }

        return Math.Max(1, count);
    }

    private static bool TryParsePlannerTime(string input, out TimeSpan time)
    {
        input = input.Trim();

        return TimeSpan.TryParseExact(input, @"hh\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParseExact(input, @"h\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParse(input, CultureInfo.CurrentCulture, out time);
    }

    private static Page? GetHostPage() => Shell.Current?.CurrentPage ?? Application.Current?.MainPage;

    private static async Task ShowPlannerAlertAsync(string title, string message)
    {
        var page = GetHostPage();
        if (page != null)
            await page.DisplayAlert(title, message, "OK");
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
