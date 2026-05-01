using Points.Models;
using Points.ViewModels.Leaderboard;

namespace Points.Views.Leaderboard;

internal sealed class LeaderboardPlannerContentRenderer
{
    private const double TimeRailWidth = 56;
    private const double LaneWidth = 286;
    private const double LaneGap = 8;
    private const double SubLaneGap = 4;
    private const double SubLaneWidth = (LaneWidth - SubLaneGap) / 2;
    private const double TimelineWidth = TimeRailWidth + LaneWidth + LaneGap + LaneWidth;

    private readonly LeaderboardViewModel _viewModel;
    private readonly LeaderboardPlannerInteractionCoordinator _plannerInteractions;

    public LeaderboardPlannerContentRenderer(
        LeaderboardViewModel viewModel,
        LeaderboardPlannerInteractionCoordinator plannerInteractions)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _plannerInteractions = plannerInteractions ?? throw new ArgumentNullException(nameof(plannerInteractions));
    }

    public View Build()
    {
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

        var layer = BuildTimelineLayer(out var rebuildTimeline);

        _viewModel.PlannerTimelineItems.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(rebuildTimeline);
        _viewModel.PlannerTimeGuides.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(rebuildTimeline);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LeaderboardViewModel.PlannerContentHeight))
                MainThread.BeginInvokeOnMainThread(rebuildTimeline);
        };

        rebuildTimeline();

        root.Add(BuildControls(), 0, 0);
        root.Add(BuildActions(), 0, 1);
        root.Add(BuildLaneHeader(), 0, 2);
        root.Add(layer, 0, 3);

        return root;
    }

    private static Grid BuildControls()
    {
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

        return controls;
    }

    private Grid BuildActions()
    {
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
        addTask.Clicked += async (_, _) => await _plannerInteractions.AddPlannerTaskAsync();

        var addEvent = CreatePlannerActionButton("Add Event");
        addEvent.Clicked += async (_, _) => await _plannerInteractions.AddPlannerEventAsync();

        actions.Add(summary, 0, 0);
        actions.Add(addTask, 1, 0);
        actions.Add(addEvent, 2, 0);

        return actions;
    }

    private static Grid BuildLaneHeader()
    {
        var laneHeader = new Grid
        {
            WidthRequest = TimelineWidth,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(TimeRailWidth)),
                new ColumnDefinition(new GridLength(SubLaneWidth)),
                new ColumnDefinition(new GridLength(SubLaneGap)),
                new ColumnDefinition(new GridLength(SubLaneWidth)),
                new ColumnDefinition(new GridLength(LaneGap)),
                new ColumnDefinition(new GridLength(SubLaneWidth)),
                new ColumnDefinition(new GridLength(SubLaneGap)),
                new ColumnDefinition(new GridLength(SubLaneWidth))
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

        return laneHeader;
    }

    private Grid BuildTimelineLayer(out Action rebuildTimeline)
    {
        var layer = new Grid();
        var timeline = new AbsoluteLayout
        {
            WidthRequest = TimelineWidth,
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

        layer.Add(scroll);
        layer.Add(busy);
        layer.Add(empty);
        layer.Add(error);

        var capturedTimeline = timeline;
        rebuildTimeline = () => RebuildTimeline(capturedTimeline);

        return layer;
    }

    private void RebuildTimeline(AbsoluteLayout timeline)
    {
        timeline.Children.Clear();
        timeline.HeightRequest = _viewModel.PlannerContentHeight;

        AddTimelineLaneBackground(timeline, _viewModel.PlannerContentHeight);

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
                AbsoluteLayout.SetLayoutBounds(label, new Rect(0, Math.Max(0, guide.Top - 8), TimeRailWidth - 6, 18));
                timeline.Children.Add(label);
            }

            var line = new BoxView
            {
                HeightRequest = guide.IsMajor ? 1 : 0.5,
                BackgroundColor = guide.IsMajor ? Color.FromArgb("#5A5A5A") : Color.FromArgb("#383838"),
                Opacity = guide.IsMajor ? 0.7 : 0.45
            };
            AbsoluteLayout.SetLayoutBounds(line, new Rect(TimeRailWidth, guide.Top, TimelineWidth - TimeRailWidth, 1));
            timeline.Children.Add(line);
        }

        foreach (var item in _viewModel.PlannerTimelineItems)
        {
            var left = item.Lane == PlannerTimelineLane.Tasks
                ? TimeRailWidth
                : TimeRailWidth + LaneWidth + LaneGap;

            left += item.IsPlanned ? 0 : SubLaneWidth + SubLaneGap;

            var block = CreatePlannerTimelineBlock(item);
            AbsoluteLayout.SetLayoutBounds(block, new Rect(left + 3, item.Top, SubLaneWidth - 6, item.Height));
            timeline.Children.Add(block);
        }
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

    private static void AddTimelineLaneBackground(AbsoluteLayout timeline, double height)
    {
        var taskLane = new BoxView
        {
            BackgroundColor = Color.FromArgb("#171717"),
            Opacity = 0.92
        };
        AbsoluteLayout.SetLayoutBounds(taskLane, new Rect(TimeRailWidth, 0, LaneWidth, height));
        timeline.Children.Add(taskLane);

        var eventLane = new BoxView
        {
            BackgroundColor = Color.FromArgb("#171717"),
            Opacity = 0.92
        };
        AbsoluteLayout.SetLayoutBounds(eventLane, new Rect(TimeRailWidth + LaneWidth + LaneGap, 0, LaneWidth, height));
        timeline.Children.Add(eventLane);

        AddVerticalDivider(timeline, TimeRailWidth + SubLaneWidth + (SubLaneGap / 2), height);
        AddVerticalDivider(timeline, TimeRailWidth + LaneWidth + LaneGap + SubLaneWidth + (SubLaneGap / 2), height);
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
            Command = new Command(async () => await _plannerInteractions.OnPlannerTimelineItemTappedAsync(item))
        });

        return frame;
    }
}
