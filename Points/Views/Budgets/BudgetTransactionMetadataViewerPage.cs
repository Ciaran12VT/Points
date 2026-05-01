using Points.Models;
using Points.Services.Navigation;
using Points.ViewModels.Budgets;

namespace Points.Views.Budgets;

public sealed class BudgetTransactionMetadataViewerPage : ContentPage
{
    public BudgetTransactionMetadataViewerPage(
        IReadOnlyList<UdmdTransModel> metadata,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        Title = "Metadata";

        var viewModel = new BudgetTransactionMetadataViewerViewModel(metadata, navigation, dialogs);
        BindingContext = viewModel;

        var rows = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() => CreateMetadataRow(viewModel))
        };
        rows.SetBinding(ItemsView.ItemsSourceProperty, nameof(BudgetTransactionMetadataViewerViewModel.Rows));

        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Colors.Gray,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            CornerRadius = 12
        };
        closeButton.SetBinding(Button.CommandProperty, nameof(BudgetTransactionMetadataViewerViewModel.CloseCommand));

        var layout = new Grid
        {
            Padding = 16,
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        layout.Children.Add(rows);
        Grid.SetRow(rows, 0);

        layout.Children.Add(closeButton);
        Grid.SetRow(closeButton, 1);

        Content = layout;
    }

    private static View CreateMetadataRow(BudgetTransactionMetadataViewerViewModel viewModel)
    {
        var fieldLabel = new Label
        {
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };
        fieldLabel.SetBinding(Label.TextProperty, nameof(BudgetTransactionMetadataRow.FieldName));

        var valueLabel = new Label
        {
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };
        valueLabel.SetBinding(Label.TextProperty, nameof(BudgetTransactionMetadataRow.DisplayValue));

        var viewButton = new Button
        {
            Text = "View",
            BackgroundColor = Colors.DodgerBlue,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10,
            WidthRequest = 90,
            HeightRequest = 40,
            Command = viewModel.OpenImageCommand
        };
        viewButton.SetBinding(IsVisibleProperty, nameof(BudgetTransactionMetadataRow.IsImage));
        viewButton.SetBinding(IsEnabledProperty, nameof(BudgetTransactionMetadataRow.CanViewImage));
        viewButton.SetBinding(Button.CommandParameterProperty, ".");

        var valueRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10,
            Children = { valueLabel, viewButton }
        };
        Grid.SetColumn(viewButton, 1);

        return new VerticalStackLayout
        {
            Padding = new Thickness(0, 8),
            Spacing = 4,
            Children = { fieldLabel, valueRow }
        };
    }
}

public sealed class BudgetTransactionMetadataImagePage : ContentPage
{
    public BudgetTransactionMetadataImagePage(BudgetTransactionMetadataImageViewModel viewModel)
    {
        BindingContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        SetBinding(TitleProperty, new Binding(nameof(BudgetTransactionMetadataImageViewModel.Title)));

        var image = new Image
        {
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        image.SetBinding(Image.SourceProperty, nameof(BudgetTransactionMetadataImageViewModel.ImagePath));

        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Colors.Gray,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            CornerRadius = 12
        };
        closeButton.SetBinding(Button.CommandProperty, nameof(BudgetTransactionMetadataImageViewModel.CloseCommand));

        var layout = new Grid
        {
            Padding = 12,
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        layout.Children.Add(image);
        Grid.SetRow(image, 0);

        layout.Children.Add(closeButton);
        Grid.SetRow(closeButton, 1);

        Content = layout;
    }
}
