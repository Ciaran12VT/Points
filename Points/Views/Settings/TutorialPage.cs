using Points.Services.Premium;

namespace Points.Views.Settings;

public sealed class TutorialPage : ContentPage
{
    private static readonly Color BorderColor = Color.FromArgb("#DDDDDD");
    private static readonly Color MutedTextColor = Color.FromArgb("#666666");
    private static readonly Color PaleBackgroundColor = Color.FromArgb("#F5F5F5");
    private static readonly Color PremiumLabelColor = Color.FromArgb("#D9A800");

    private readonly IPremiumSubscriptionService _premiumSubscriptions;
    private readonly ContentView _contentHost = new();
    private readonly Grid _mainTabs;
    private readonly Button _overviewTab;
    private readonly Button _featuresTab;
    private readonly IReadOnlyList<TutorialFeature> _features;
    private bool _hasLoadedPremiumState;
    private bool _hasPremium;
    private MainTutorialTab _selectedMainTab = MainTutorialTab.Overview;
    private TutorialFeature? _selectedFeature;
    private FeatureTutorialTab _selectedFeatureTab = FeatureTutorialTab.Description;

    public TutorialPage(IPremiumSubscriptionService premiumSubscriptions)
    {
        _premiumSubscriptions = premiumSubscriptions ?? throw new ArgumentNullException(nameof(premiumSubscriptions));
        _features = CreateFeatures();

        Title = "Tutorial";

        _overviewTab = CreateTabButton("Overview", () =>
        {
            _selectedFeature = null;
            _selectedMainTab = MainTutorialTab.Overview;
            Render();
        });

        _featuresTab = CreateTabButton("Features", () =>
        {
            _selectedFeature = null;
            _selectedMainTab = MainTutorialTab.Features;
            Render();
        });

        _mainTabs = new Grid
        {
            Padding = new Thickness(12, 12, 12, 6),
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Children = { _overviewTab, _featuresTab }
        };
        Microsoft.Maui.Controls.Grid.SetColumn(_featuresTab, 1);

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            Children = { _mainTabs, _contentHost }
        };
        Microsoft.Maui.Controls.Grid.SetRow(_contentHost, 1);

        Render();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoadedPremiumState)
            return;

        _hasLoadedPremiumState = true;
        _hasPremium = await _premiumSubscriptions.HasPremiumAsync();
        Render();
    }

    private void Render()
    {
        _mainTabs.IsVisible = _selectedFeature == null;
        UpdateMainTabStyles();

        if (_selectedFeature != null)
        {
            _contentHost.Content = BuildFeatureDetail(_selectedFeature);
            return;
        }

        _contentHost.Content = _selectedMainTab == MainTutorialTab.Overview
            ? BuildOverview()
            : BuildFeatureGrid();
    }

    private ScrollView BuildOverview()
    {
        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(16, 10, 16, 24),
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = "How Points Works",
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 24,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                BuildOverviewSection(
                    "The Points System",
                    "Points measures the value of what you are doing right now. Cards can add points, spend points, or track progress over time, so the home screen becomes a live readout of effort, cost, and momentum.",
                    BuildPointsSystemScreenshot()),
                BuildOverviewSection(
                    "Notifications",
                    "Notifications keep the system visible when the app is not in front of you. Active-card notifications remind you what is currently running, while scheduled notifications surface cards when their configured time arrives.",
                    BuildNotificationsScreenshot()),
                BuildOverviewSection(
                    "Active and Inactive",
                    "An active card is currently counting time and changing its point value. Inactive cards stay available without changing until you start them again. Only one active card should be treated as the current focus.",
                    BuildActiveStateScreenshot())
            }
        };

        return new ScrollView { Content = stack };
    }

    private ScrollView BuildFeatureGrid()
    {
        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(12, 10, 12, 24),
            Spacing = 18
        };

        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 18,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        for (var i = 0; i < _features.Count; i++)
        {
            if (i % 4 == 0)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var feature = _features[i];
            var item = BuildFeatureButton(feature);

            grid.Children.Add(item);
            Microsoft.Maui.Controls.Grid.SetColumn(item, i % 4);
            Microsoft.Maui.Controls.Grid.SetRow(item, i / 4);
        }

        stack.Children.Add(grid);

        return new ScrollView { Content = stack };
    }

    private View BuildFeatureDetail(TutorialFeature feature)
    {
        var backButton = new Button
        {
            Text = "< Back",
            BackgroundColor = Colors.Black,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 18,
            HeightRequest = 42,
            Padding = new Thickness(14, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        backButton.Clicked += (_, _) =>
        {
            _selectedFeature = null;
            _selectedFeatureTab = FeatureTutorialTab.Description;
            Render();
        };

        var titleLabel = new Label
        {
            Text = feature.Name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 22,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            Children = { backButton, titleLabel }
        };
        Microsoft.Maui.Controls.Grid.SetColumn(titleLabel, 1);

        var descriptionTab = CreateTabButton("Description", () =>
        {
            _selectedFeatureTab = FeatureTutorialTab.Description;
            Render();
        });
        var howToTab = CreateTabButton("How To", () =>
        {
            _selectedFeatureTab = FeatureTutorialTab.HowTo;
            Render();
        });

        ApplyTabStyle(descriptionTab, _selectedFeatureTab == FeatureTutorialTab.Description);
        ApplyTabStyle(howToTab, _selectedFeatureTab == FeatureTutorialTab.HowTo);

        var tabs = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Children = { descriptionTab, howToTab }
        };
        Microsoft.Maui.Controls.Grid.SetColumn(howToTab, 1);

        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(16, 12, 16, 24),
            Spacing = 14,
            Children = { titleRow, tabs }
        };

        if (_selectedFeatureTab == FeatureTutorialTab.Description)
            AddFeatureDescription(stack, feature);
        else
            AddFeatureHowTo(stack, feature);

        return new ScrollView { Content = stack };
    }

    private void AddFeatureDescription(VerticalStackLayout stack, TutorialFeature feature)
    {
        if (!string.IsNullOrWhiteSpace(feature.Image))
            stack.Children.Add(BuildImageScreenshot(feature.Image, feature.DescriptionImageHeight));

        if (feature.ShowGeneratedScreenshot)
            stack.Children.Add(BuildFeatureScreenshot(feature));

        foreach (var paragraph in feature.DescriptionParagraphs)
        {
            stack.Children.Add(new Label
            {
                Text = paragraph,
                FontSize = 15,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }
    }

    private static void AddFeatureHowTo(VerticalStackLayout stack, TutorialFeature feature)
    {
        var card = new Frame
        {
            Padding = 14,
            CornerRadius = 12,
            HasShadow = false,
            BorderColor = BorderColor,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Configure in the app",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 18
                    }
                }
            }
        };

        var steps = (VerticalStackLayout)card.Content;
        for (var i = 0; i < feature.HowToSteps.Count; i++)
        {
            steps.Children.Add(new Label
            {
                Text = $"{i + 1}. {feature.HowToSteps[i]}",
                FontSize = 15,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        stack.Children.Add(card);
    }

    private View BuildFeatureButton(TutorialFeature feature)
    {
        var isDimmedPremium = feature.IsPremium && !_hasPremium;
        var iconColor = isDimmedPremium
            ? Color.FromArgb("#D8D8D8")
            : feature.IconColor ?? Colors.White;

        var button = new Button
        {
            Text = feature.Icon,
            BackgroundColor = Colors.Black,
            TextColor = iconColor,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 34,
            WidthRequest = 68,
            HeightRequest = 68,
            MinimumWidthRequest = 68,
            MinimumHeightRequest = 68,
            Padding = 0,
            HorizontalOptions = LayoutOptions.Center
        };
        button.Clicked += (_, _) =>
        {
            _selectedFeature = feature;
            _selectedFeatureTab = FeatureTutorialTab.Description;
            Render();
        };

        var name = new Label
        {
            Text = feature.Name,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2
        };

        var premium = new Label
        {
            Text = "Premium",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = PremiumLabelColor,
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = feature.IsPremium
        };

        return new VerticalStackLayout
        {
            Spacing = 5,
            Children = { button, name, premium }
        };
    }

    private static Frame BuildOverviewSection(string title, string body, View visual)
    {
        return new Frame
        {
            Padding = 14,
            CornerRadius = 12,
            HasShadow = false,
            BorderColor = BorderColor,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 19
                    },
                    new Label
                    {
                        Text = body,
                        FontSize = 15,
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    visual
                }
            }
        };
    }

    private static View BuildPointsSystemScreenshot()
    {
        return BuildMockScreenshot(
            "Home",
            [
                ("Main Quest", "+2.0 / min", Colors.Green),
                ("Focus sprint", "+18 pts", Colors.Green),
                ("Distraction", "-5 pts", Colors.DarkRed)
            ]);
    }

    private static View BuildNotificationsScreenshot()
    {
        var card = new Frame
        {
            Padding = 12,
            CornerRadius = 16,
            HasShadow = false,
            BorderColor = BorderColor,
            BackgroundColor = PaleBackgroundColor,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = "Points",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 12,
                        TextColor = Colors.Black
                    },
                    new Label
                    {
                        Text = "Focus sprint is active",
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 16,
                        TextColor = Colors.Black
                    },
                    new Label
                    {
                        Text = "Tap to return to your current card.",
                        FontSize = 13,
                        TextColor = MutedTextColor
                    },
                    BuildNotificationRow("Scheduled", "Planning review is ready")
                }
            }
        };

        return card;
    }

    private static View BuildActiveStateScreenshot()
    {
        var activeCard = BuildStateCard("Active", "Focus sprint", "+2.0 / min", Colors.Green);
        var inactiveCard = BuildStateCard("Inactive", "Reading", "Paused", Colors.Gray);

        var grid = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Children = { activeCard, inactiveCard }
        };
        Microsoft.Maui.Controls.Grid.SetColumn(inactiveCard, 1);

        return grid;
    }

    private static View BuildMockScreenshot(string title, IReadOnlyList<(string Title, string Value, Color Color)> rows)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 13,
                    TextColor = Colors.Black
                }
            }
        };

        foreach (var row in rows)
            stack.Children.Add(BuildMiniCard(row.Title, row.Value, row.Color));

        return new Frame
        {
            Padding = 12,
            CornerRadius = 16,
            HasShadow = false,
            BorderColor = BorderColor,
            BackgroundColor = PaleBackgroundColor,
            Content = stack
        };
    }

    private static View BuildFeatureScreenshot(TutorialFeature feature)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = feature.Name,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 13,
                    TextColor = Colors.Black
                },
                BuildMiniCard(feature.ScreenshotTitle, feature.ScreenshotMetric, Colors.Green)
            }
        };

        foreach (var highlight in feature.ScreenshotHighlights)
            stack.Children.Add(BuildMiniCard(highlight, "Ready", Colors.Black));

        return new Frame
        {
            Padding = 12,
            CornerRadius = 16,
            HasShadow = false,
            BorderColor = BorderColor,
            BackgroundColor = PaleBackgroundColor,
            Content = stack
        };
    }

    private static View BuildImageScreenshot(string image, double heightRequest)
    {
        return new Frame
        {
            Padding = 0,
            CornerRadius = 14,
            HasShadow = false,
            IsClippedToBounds = true,
            BackgroundColor = Colors.Black,
            BorderColor = Colors.Black,
            HeightRequest = heightRequest,
            Content = new Image
            {
                Source = ImageSource.FromFile(image),
                Aspect = Aspect.AspectFit,
                BackgroundColor = Colors.Black
            }
        };
    }

    private static View BuildMiniCard(string title, string value, Color accent)
    {
        var dot = new BoxView
        {
            Color = accent,
            WidthRequest = 10,
            HeightRequest = 10,
            CornerRadius = 5,
            VerticalOptions = LayoutOptions.Center
        };

        var titleLabel = new Label
        {
            Text = title,
            FontSize = 13,
            TextColor = Colors.Black,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var valueLabel = new Label
        {
            Text = value,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = accent,
            VerticalTextAlignment = TextAlignment.Center
        };

        var grid = new Grid
        {
            Padding = new Thickness(10, 8),
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { dot, titleLabel, valueLabel }
        };
        Microsoft.Maui.Controls.Grid.SetColumn(titleLabel, 1);
        Microsoft.Maui.Controls.Grid.SetColumn(valueLabel, 2);

        return new Frame
        {
            Padding = 0,
            CornerRadius = 10,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E6E6E6"),
            BackgroundColor = Colors.White,
            Content = grid
        };
    }

    private static View BuildNotificationRow(string title, string body)
    {
        return new Frame
        {
            Padding = new Thickness(10, 8),
            CornerRadius = 10,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E6E6E6"),
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 12,
                        TextColor = Colors.Black
                    },
                    new Label
                    {
                        Text = body,
                        FontSize = 12,
                        TextColor = MutedTextColor
                    }
                }
            }
        };
    }

    private static View BuildStateCard(string state, string cardName, string value, Color accent)
    {
        return new Frame
        {
            Padding = 12,
            CornerRadius = 14,
            HasShadow = false,
            BorderColor = BorderColor,
            BackgroundColor = state == "Active" ? Color.FromArgb("#EBF8EF") : PaleBackgroundColor,
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label
                    {
                        Text = state,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 13,
                        TextColor = accent
                    },
                    new Label
                    {
                        Text = cardName,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 15,
                        TextColor = Colors.Black,
                        LineBreakMode = LineBreakMode.TailTruncation
                    },
                    new Label
                    {
                        Text = value,
                        FontSize = 12,
                        TextColor = MutedTextColor
                    }
                }
            }
        };
    }

    private Button CreateTabButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            CornerRadius = 18,
            HeightRequest = 42,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(10, 0)
        };
        button.Clicked += (_, _) => action();
        return button;
    }

    private void UpdateMainTabStyles()
    {
        ApplyTabStyle(_overviewTab, _selectedMainTab == MainTutorialTab.Overview);
        ApplyTabStyle(_featuresTab, _selectedMainTab == MainTutorialTab.Features);
    }

    private static void ApplyTabStyle(Button button, bool isSelected)
    {
        button.BackgroundColor = isSelected ? Colors.Black : Color.FromArgb("#E7E7E7");
        button.TextColor = isSelected ? Colors.White : Colors.Black;
    }

    private static IReadOnlyList<TutorialFeature> CreateFeatures()
    {
        return
        [
            new TutorialFeature
            {
                Name = "TAT Cards",
                Icon = "🕑",
                Image = "tutorial_tat_cards.png",
                DescriptionImageHeight = 360,
                ShowGeneratedScreenshot = false,
                ScreenshotTitle = "Reading",
                ScreenshotMetric = "+1.0 / min",
                ScreenshotHighlights = ["Active toggle", "Time tracked", "Daily value"],
                DescriptionParagraphs =
                [
                    "TAT (Time-At-Task) Cards let you track how long you spend on a task and associate value with time spent. These are great for tracking tasks like reading, studying, working out, household chores, or walking the dog. You define how much value you should earn per minute of the task, and then just tap the Active button to toggle the task on or off.",
                    "Negative time-sinks, like doom-scrolling, video games, or TV, can also be effectively tracked and punished this way."
                ],
                HowToSteps =
                [
                    "Open the Main Quest page from home.",
                    "Tap Add Card and create a TAT card for the activity.",
                    "Set the value per minute, save the card, then use Active to start and stop tracking."
                ]
            },
            new TutorialFeature
            {
                Name = "SC Cards",
                Icon = "✔",
                IconColor = Colors.Green,
                Image = "tutorial_sc_cards.png",
                DescriptionImageHeight = 560,
                ShowGeneratedScreenshot = false,
                ScreenshotTitle = "Weight Lifting",
                ScreenshotMetric = "24 reps",
                ScreenshotHighlights = ["Steps", "Repetitions", "Score update"],
                DescriptionParagraphs =
                [
                    "SC (Step-Completion) Cards allow you to define a series of steps that constitute a task and track your progress. A perfect example is weight lifting: create your Weight Lifting SC Card, create one step per exercise, and track your reps. You can also track a morning checklist of actions to perform, such as showering, brushing teeth, and moisturizing.",
                    "You assign a value per repetition of a given step and the app calculates the total value earned from them that day and updates your score."
                ],
                HowToSteps =
                [
                    "Open the Main Quest page from home.",
                    "Tap Add Card and create an SC card.",
                    "Add one step for each exercise or checklist item, set each step value, then record completions from the card."
                ]
            },
            new TutorialFeature
            {
                Name = "Missions",
                Icon = "⚑",
                Image = "tutorial_mission_cards.png",
                DescriptionImageHeight = 840,
                ShowGeneratedScreenshot = false,
                ScreenshotTitle = "Next mission",
                ScreenshotMetric = "Due today",
                ScreenshotHighlights = ["Available from", "Due by", "Event time"],
                DescriptionParagraphs =
                [
                    "Missions represent ad hoc once-off tasks that can be completed and knocked off your to-do list. The app lets you define when the task becomes available to work on, when it is due, and you can associate exact event times, like appointments or social events, with them if desired.",
                    "Missions can earn you a glut of points all at once when complete, or punish you when failed or overdue. Try the Degrade and Rot mission types to challenge yourself to stick with them to the end."
                ],
                HowToSteps =
                [
                    "Open the Mission page from home.",
                    "Tap Add Card, then set the mission title, type, value, availability, due date, and optional event time.",
                    "Use Complete when the work is done, or let the mission rules apply if it degrades, rots, fails, or becomes overdue."
                ]
            },
            new TutorialFeature
            {
                Name = "Leaderboard",
                Icon = "🔢",
                ScreenshotTitle = "Leaderboard",
                ScreenshotMetric = "Ranked",
                ScreenshotHighlights = ["Global Score", "Dead-Air", "Time ranked"],
                DescriptionParagraphs =
                [
                    "By tapping the Global Score at the top-right of the main page, you will open the Leaderboard where all tasks you have spent time on today are ranked against one another. This gives you a unique view on where your time is really going.",
                    "The Dead-Air score will show you how much untracked time you have, a metric you can work on reducing to give you both better visibility on your time and motivation to spend it wisely."
                ],
                HowToSteps =
                [
                    "Tap the Global Score at the top-right of the main page.",
                    "Use the Leaderboard tab to compare tasks by time and points.",
                    "Watch the Dead-Air row to understand how much of the day is still untracked."
                ]
            },
            new TutorialFeature
            {
                Name = "Budgets",
                Icon = "💰",
                IsPremium = true,
                Image = "tutorial_budget_cards.png",
                DescriptionImageHeight = 860,
                ShowGeneratedScreenshot = false,
                ScreenshotTitle = "Calorie Budget",
                ScreenshotMetric = "Remaining",
                ScreenshotHighlights = ["Top-up schedule", "Spend", "Overspend penalty"],
                DescriptionParagraphs =
                [
                    "Budget Cards let you control how you spend finite resources. Think of each card like a bank account where you set up a top-up schedule and draw down from it when needed. Just do not overspend, or you will be punished.",
                    "Calorie tracking is a perfect example: set up a Budget with a currency of kcal, give yourself a top-up schedule for how many calories per day you are targeting, then spend those whenever you eat. You will see the budget bar go up when you get a top-up and go down when you spend. Overspending detracts points from your global score, and that persists until your top-ups put you back in the green."
                ],
                HowToSteps =
                [
                    "Open the Budgets module from home.",
                    "Add a budget card, then configure the currency, starting balance, and top-up schedule.",
                    "Use Spend when you draw from the budget and watch the budget bar for overspending."
                ]
            },
            new TutorialFeature
            {
                Name = "Arcs",
                Icon = "∿",
                IconColor = Colors.Purple,
                IsPremium = true,
                Image = "premium_arcs.jpg",
                ScreenshotTitle = "Body Weight",
                ScreenshotMetric = "Trend",
                ScreenshotHighlights = ["Regular input", "Graph", "Simple trend"],
                DescriptionParagraphs =
                [
                    "Sometimes we just want to track a metric without punishing or incentivizing. Arcs let you plot data points on a graph based on regular input from the user.",
                    "Whether it is tracking your bank balance, body weight, waist size, or your heaviest lift in the gym, Arcs visualize this for you in a simple, immediately understandable way."
                ],
                HowToSteps =
                [
                    "Enable Arcs in Settings, then Modules & Features.",
                    "Open the Arcs page from the home carousel.",
                    "Create or open an arc, then add data points whenever you want the graph updated."
                ]
            },
            new TutorialFeature
            {
                Name = "Goals",
                Icon = "☰",
                IconColor = Colors.DodgerBlue,
                IsPremium = true,
                Image = "premium_goals.jpg",
                ScreenshotTitle = "Reading Goal",
                ScreenshotMetric = "On track",
                ScreenshotHighlights = ["Daily", "Weekly", "Progress status"],
                DescriptionParagraphs =
                [
                    "Life can get busy, and with a lot of tasks we might need a visual way to call out how we are performing on the really key things. Goals can be set up for any existing TAT or SC card and let you define a target to reach on a daily, weekly, monthly, or yearly basis.",
                    "Goals quickly tell you if you are on track, ahead of schedule, or falling behind on a given task based on your stated goal."
                ],
                HowToSteps =
                [
                    "Open Goals from the home screen.",
                    "Create a goal and choose the TAT or SC card it should track.",
                    "Set the target amount and cadence, then review whether the goal is on track from the Goals page."
                ]
            },
            new TutorialFeature
            {
                Name = "Achievements",
                Icon = "🏆",
                IsPremium = true,
                Image = "tutorial_achievements.png",
                DescriptionImageHeight = 900,
                ShowGeneratedScreenshot = false,
                ScreenshotTitle = "Reading Streak",
                ScreenshotMetric = "Pinned",
                ScreenshotHighlights = ["Difficulty", "Trophies", "Conditions"],
                DescriptionParagraphs =
                [
                    "Hard work should be given recognition, and Achievements are the way we do that. You can create Achievements which will be earned once a given condition is met, such as 80 hours of reading within a 30 day period, or all your planned workouts completed for the last 7 days.",
                    "You can rank your achievements by difficulty, pin the ones you are currently working towards, and upload images or files to earn as trophies, viewable in the in-app Trophy Room."
                ],
                HowToSteps =
                [
                    "Open Achievements from the home screen.",
                    "Create an achievement and choose the condition, range, difficulty, and reward files or images.",
                    "Pin achievements you are actively pursuing and view earned trophies in the Trophy Room."
                ]
            },
            new TutorialFeature
            {
                Name = "Reports",
                Icon = "📊",
                IsPremium = true,
                Image = "premium_reports.jpg",
                ScreenshotTitle = "SQL Report",
                ScreenshotMetric = "Insights",
                ScreenshotHighlights = ["Behaviour data", "Queries", "Review"],
                DescriptionParagraphs =
                [
                    "As time goes by and you use the app, you are generating a mountain of useful data on your behaviour.",
                    "Leverage the Reports functionality to create and run SQL reports to pull out insights into your life you never thought possible."
                ],
                HowToSteps =
                [
                    "Open Reports from the home screen.",
                    "Create or open a SQL report.",
                    "Run the report and use the results to spot patterns in your Points data."
                ]
            },
            new TutorialFeature
            {
                Name = "Value Rates",
                Icon = "📈",
                IsPremium = true,
                ScreenshotTitle = "Reading",
                ScreenshotMetric = "Non-fiction",
                ScreenshotHighlights = ["Named rates", "Prompt", "Different values"],
                DescriptionParagraphs =
                [
                    "Associated with TAT cards, this functionality allows you to define multiple named rates for a given card. Say I have a Reading card, but I want to track what kind of reading I am doing and value each kind differently.",
                    "Maybe I like to read fiction, but struggle with non-fiction, and I want to encourage myself to make the effort. Set a value rate for each kind under the Reading card, then whenever I activate that card it will prompt me to choose which kind I am doing. I will give myself more points for non-fiction for having made the effort."
                ],
                HowToSteps =
                [
                    "Enable Value Rates in Settings, then Modules & Features.",
                    "Open a TAT card and add the named rates you want to choose from.",
                    "When activating the card, choose the rate that matches what you are doing."
                ]
            },
            new TutorialFeature
            {
                Name = "Metadata Fields",
                Icon = "i",
                IconColor = Colors.Orange,
                IsPremium = true,
                ScreenshotTitle = "Food photo",
                ScreenshotMetric = "Stored",
                ScreenshotHighlights = ["Text", "Dropdown", "Image"],
                DescriptionParagraphs =
                [
                    "Sometimes the data we gather just is not enough. If I tap Spend on my Calorie budget card, it will prompt me for the calories value, but what if I want to track which food it was, or take a picture of the food and store it along with the spend transaction?",
                    "In come Metadata Fields: user-defined fields that can let you type text, choose from a dropdown menu, enter a number, or take a picture and associate it with a Budget Card spend or a TAT Card active session."
                ],
                HowToSteps =
                [
                    "Open a supported card details page.",
                    "Tap Metadata Fields and add the fields you want to collect.",
                    "The app will prompt for those fields during supported actions such as spending from a Budget card or starting a TAT session."
                ]
            },
            new TutorialFeature
            {
                Name = "Locks",
                Icon = "🔒",
                IsPremium = true,
                ScreenshotTitle = "Watch TV",
                ScreenshotMetric = "Locked",
                ScreenshotHighlights = ["Condition", "Reading first", "Unlock"],
                DescriptionParagraphs =
                [
                    "With bad habits, or just indulgences we tend to over-indulge in, we sometimes need a firm hand. Locks let you prevent yourself from being able to activate a card until a condition is met.",
                    "Say I have a habit of watching a little too much TV when I really should be reading more. I will set a lock on the Watch TV TAT card which says I cannot activate it until 1 hour of Reading has been done that day first."
                ],
                HowToSteps =
                [
                    "Enable Locks in Settings, then Modules & Features.",
                    "Open the card you want to restrict and configure its Locks section.",
                    "Add the prerequisite or time-window condition, then save the card."
                ]
            },
            new TutorialFeature
            {
                Name = "Reminders",
                Icon = "📅",
                IsPremium = true,
                ScreenshotTitle = "Household Chores",
                ScreenshotMetric = "Wednesday",
                ScreenshotHighlights = ["Custom message", "Card link", "Notification"],
                DescriptionParagraphs =
                [
                    "With Reminders, you can set up a notification with a custom message associated with a given card.",
                    "If I have a Household Chores card and I know I have got to take out the trash on Wednesdays, I can set a reminder for a particular time on Wednesdays associated with the Household Chores card with the message Trash goes out today."
                ],
                HowToSteps =
                [
                    "Enable Schedules in Settings, then Modules & Features.",
                    "Open the card that should remind you.",
                    "Add a reminder or schedule notification with the time and custom message you want."
                ]
            },
            new TutorialFeature
            {
                Name = "Timers",
                Icon = "⏰",
                IsPremium = true,
                ScreenshotTitle = "Reading",
                ScreenshotMetric = "1 hour",
                ScreenshotHighlights = ["Daily target", "Ping", "Accumulated time"],
                DescriptionParagraphs =
                [
                    "If you are in danger of getting lost in a task, set a Timer for it. With Timers, you define the expected amount of time you should spend on a task in a day and once that is met it will send you a notification.",
                    "If I want to read for an hour a day, even if that hour is broken up into separate chunks over the day, once the 1 hour mark is hit I will get a gentle ping to let me know I should probably get on to something else now."
                ],
                HowToSteps =
                [
                    "Open the TAT card you want to time.",
                    "Set the target or expected active time for the day.",
                    "Activate the card as normal; the timer notification fires once the accumulated active time reaches the target."
                ]
            },
            new TutorialFeature
            {
                Name = "Planner",
                Icon = "📅",
                IsPremium = true,
                ScreenshotTitle = "Today",
                ScreenshotMetric = "Planned",
                ScreenshotHighlights = ["Time blocks", "Actuals", "Gaps"],
                DescriptionParagraphs =
                [
                    "By tapping the Global Score at the top-right of the main page, you open the area containing the Leaderboard and also the Planner.",
                    "The Planner is a great way to quickly, visually organize your intended time blocks for the day, and compare what you actually did against that plan. This is not meant as a firm commitment, but rather a guide for when you are at a loss for what to do next. The visual presentation can also help you think through what gaps in your agenda you have and what can be achieved during that time."
                ],
                HowToSteps =
                [
                    "Tap the Global Score at the top-right of the main page.",
                    "Switch to the Planner tab.",
                    "Add intended time blocks, then compare the plan against what actually happened during the day."
                ]
            }
        ];
    }

    private enum MainTutorialTab
    {
        Overview,
        Features
    }

    private enum FeatureTutorialTab
    {
        Description,
        HowTo
    }

    private sealed class TutorialFeature
    {
        public string Name { get; init; } = "";
        public string Icon { get; init; } = "";
        public Color? IconColor { get; init; }
        public bool IsPremium { get; init; }
        public string Image { get; init; } = "";
        public double DescriptionImageHeight { get; init; } = 210;
        public bool ShowGeneratedScreenshot { get; init; } = true;
        public string ScreenshotTitle { get; init; } = "";
        public string ScreenshotMetric { get; init; } = "";
        public IReadOnlyList<string> ScreenshotHighlights { get; init; } = [];
        public IReadOnlyList<string> DescriptionParagraphs { get; init; } = [];
        public IReadOnlyList<string> HowToSteps { get; init; } = [];
    }
}
