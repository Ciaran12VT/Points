using CommunityToolkit.Maui.Views;
using Points.Models;

namespace Points.Views.Premium;

public enum PremiumUpgradePopupResult
{
    NotNow,
    Upgrade
}

public sealed class PremiumUpgradePopup : Popup
{
    private static readonly IReadOnlyList<PremiumFeatureSlide> Slides =
    [
        new()
        {
            Title = "Enhanced Main Quest",
            Image = "premium_mainquest.jpg",
            Description = "Shape your main quest with premium planning tools."
        },
        new()
        {
            Title = "Achievements",
            Image = "premium_achievements.jpg",
            Description = "Create challenges and track meaningful milestones."
        },
        new()
        {
            Title = "Trophies",
            Image = "premium_trophies.jpg",
            Description = "Celebrate progress with a visual trophy room."
        },
        new()
        {
            Title = "Goals",
            Image = "premium_goals.jpg",
            Description = "Turn long-term aims into visible progress."
        },
        new()
        {
            Title = "Budgets",
            Image = "premium_budgets.jpg",
            Description = "Manage point budgets, spending, and cash-in flows."
        },
        new()
        {
            Title = "Arcs",
            Image = "premium_arcs.jpg",
            Description = "Track habits and values as they change over time."
        },
        new()
        {
            Title = "Reports",
            Image = "premium_reports.jpg",
            Description = "Review deeper trends across your activity."
        }
    ];

    public PremiumUpgradePopup()
    {
        CanBeDismissedByTappingOutsideOfPopup = true;

        var size = GetPopupSize();
        var indicator = BuildIndicator();
        var carousel = BuildCarousel(indicator);

        var root = new Grid
        {
            Padding = new Thickness(14),
            WidthRequest = size.Width,
            HeightRequest = size.Height,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 12,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E1E1E")
                : Colors.White
        };

        root.Add(BuildHeader(), 0, 0);
        root.Add(carousel, 0, 1);
        root.Add(indicator, 0, 2);
        root.Add(BuildActions(), 0, 3);

        Content = new Frame
        {
            Padding = 0,
            CornerRadius = 16,
            HasShadow = true,
            IsClippedToBounds = true,
            Content = root
        };
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
            Text = "Upgrade to Premium!",
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

        close.Clicked += (_, _) => Close(PremiumUpgradePopupResult.NotNow);
        header.Add(close, 1, 0);

        return header;
    }

    private static CarouselView BuildCarousel(IndicatorView indicator)
    {
        return new CarouselView
        {
            ItemsSource = Slides,
            IndicatorView = indicator,
            IsSwipeEnabled = true,
            ItemTemplate = new DataTemplate(BuildSlide)
        };
    }

    private static object BuildSlide()
    {
        var screenshot = new Image
        {
            Aspect = Aspect.AspectFit,
            BackgroundColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        screenshot.SetBinding(Image.SourceProperty, nameof(PremiumFeatureSlide.Image));

        var screenshotFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            HasShadow = false,
            IsClippedToBounds = true,
            BackgroundColor = Colors.Black,
            Content = screenshot
        };

        var title = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        title.SetBinding(Label.TextProperty, nameof(PremiumFeatureSlide.Title));

        var description = new Label
        {
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#D6D6D6")
                : Color.FromArgb("#333333")
        };
        description.SetBinding(Label.TextProperty, nameof(PremiumFeatureSlide.Description));

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 8,
            Padding = new Thickness(0, 2)
        };

        root.Add(screenshotFrame, 0, 0);
        root.Add(title, 0, 1);
        root.Add(description, 0, 2);

        return root;
    }

    private static IndicatorView BuildIndicator()
    {
        return new IndicatorView
        {
            IndicatorColor = Colors.Gray,
            SelectedIndicatorColor = Colors.Green,
            HorizontalOptions = LayoutOptions.Center,
            IndicatorsShape = IndicatorShape.Circle,
            IndicatorSize = 8
        };
    }

    private View BuildActions()
    {
        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };

        var notNow = new Button
        {
            Text = "Not now",
            HeightRequest = 46,
            CornerRadius = 12,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#333333")
                : Color.FromArgb("#E5E5E5"),
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black
        };

        var upgrade = new Button
        {
            Text = "Upgrade",
            HeightRequest = 46,
            CornerRadius = 12,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Green,
            TextColor = Colors.White
        };

        notNow.Clicked += (_, _) => Close(PremiumUpgradePopupResult.NotNow);
        upgrade.Clicked += (_, _) => Close(PremiumUpgradePopupResult.Upgrade);

        actions.Add(notNow, 0, 0);
        actions.Add(upgrade, 1, 0);

        return actions;
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
                Math.Min(720, Math.Max(500, height - 96)));
        }
        catch
        {
            return new Size(360, 620);
        }
    }
}
