using System.Globalization;
using Points.Models;
using Points.Services.Sqlite.Interfaces;

namespace Points.Views.Details;

public sealed class UdmdConfigPage : ContentPage
{
    private readonly long _cardId;
    private readonly IUdmdService _udmd;
    private readonly VerticalStackLayout _fieldsStack = new();
    private readonly Label _messageLabel = new() { IsVisible = false, TextColor = Colors.OrangeRed };
    private readonly List<FieldEditor> _editors = new();
    private bool _loaded;

    public UdmdConfigPage(long cardId, IUdmdService udmd)
    {
        _cardId = cardId;
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));

        Title = "UDMD";

        var addButton = new Button
        {
            Text = "Add Field",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 44,
            CornerRadius = 12
        };
        addButton.Clicked += (_, __) => AddEditor(new UdmdConfigModel
        {
            CardID = _cardId,
            FieldName = "",
            FieldType = UdmdFieldType.Text.ToString(),
            DisplayOrder = _editors.Count,
            IsActive = true
        }, "");

        var saveAllButton = new Button
        {
            Text = "Save",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 21,
            HeightRequest = 48
        };
        saveAllButton.Clicked += async (_, __) => await SaveAllAsync();

        var body = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14,
            Children =
            {
                addButton,
                _messageLabel,
                _fieldsStack
            }
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            Children =
            {
                new ScrollView { Content = body },
                saveAllButton
            }
        };
        Grid.SetRow(saveAllButton, 1);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded)
            return;

        _loaded = true;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_cardId <= 0)
        {
            ShowMessage("Save the card before configuring metadata fields.");
            return;
        }

        _fieldsStack.Children.Clear();
        _editors.Clear();

        var configs = await _udmd.GetUdmdConfigsForCardAsync(_cardId);
        foreach (var config in configs)
        {
            var dropdownText = "";
            if (config.FieldTypeKind == UdmdFieldType.Dropdown)
            {
                var dropdowns = await _udmd.GetDropdownValuesAsync(config.UdmdConfigID);
                dropdownText = string.Join(Environment.NewLine, dropdowns.Select(x => x.DropdownValue));
            }

            AddEditor(config, dropdownText);
        }

        if (configs.Count == 0)
            ShowMessage("No metadata fields yet.");
        else
            HideMessage();
    }

    private void AddEditor(UdmdConfigModel config, string dropdownText)
    {
        HideMessage();

        var editor = new FieldEditor(config);
        editor.DropdownValuesEditor.Text = dropdownText;
        editor.SaveButton.Clicked += async (_, __) => await SaveEditorAsync(editor);
        editor.DeactivateButton.Clicked += async (_, __) => await DeactivateEditorAsync(editor);
        editor.TypePicker.SelectedIndexChanged += (_, __) => editor.UpdateDropdownVisibility();

        _editors.Add(editor);
        _fieldsStack.Children.Add(editor.Frame);
    }

    private async Task SaveAllAsync()
    {
        foreach (var editor in _editors.ToList())
        {
            var saved = await SaveEditorAsync(editor, showSuccess: false);
            if (!saved)
                return;
        }

        ShowMessage("Saved.", Colors.Green);
    }

    private async Task<bool> SaveEditorAsync(FieldEditor editor, bool showSuccess = true)
    {
        try
        {
            var config = editor.ReadConfig(_cardId);
            config = await _udmd.SaveUdmdConfigAsync(config);
            editor.Config = config;

            if (config.FieldTypeKind == UdmdFieldType.Dropdown)
            {
                var values = SplitDropdownValues(editor.DropdownValuesEditor.Text);
                await _udmd.SaveDropdownValuesAsync(config.UdmdConfigID, values);
            }

            if (showSuccess)
                ShowMessage("Saved.", Colors.Green);

            return true;
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message);
            return false;
        }
    }

    private async Task DeactivateEditorAsync(FieldEditor editor)
    {
        if (editor.Config.UdmdConfigID == 0)
        {
            _editors.Remove(editor);
            _fieldsStack.Children.Remove(editor.Frame);
            return;
        }

        await _udmd.DeleteOrDeactivateUdmdConfigAsync(editor.Config.UdmdConfigID);
        editor.ActiveSwitch.IsToggled = false;
        editor.Config.IsActive = false;
        ShowMessage("Field deactivated.", Colors.Green);
    }

    private static IEnumerable<string> SplitDropdownValues(string? text)
    {
        return (text ?? "")
            .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0);
    }

    private void ShowMessage(string message, Color? color = null)
    {
        _messageLabel.Text = message;
        _messageLabel.TextColor = color ?? Colors.OrangeRed;
        _messageLabel.IsVisible = true;
    }

    private void HideMessage()
    {
        _messageLabel.IsVisible = false;
        _messageLabel.Text = "";
    }

    private sealed class FieldEditor
    {
        private static readonly List<string> FieldTypes =
            Enum.GetValues(typeof(UdmdFieldType)).Cast<UdmdFieldType>().Select(x => x.ToString()).ToList();

        public UdmdConfigModel Config { get; set; }
        public Frame Frame { get; }
        public Entry NameEntry { get; } = new() { Placeholder = "Field name" };
        public Picker TypePicker { get; } = new() { Title = "Field type", ItemsSource = FieldTypes };
        public Switch RequiredSwitch { get; } = new();
        public Switch ActiveSwitch { get; } = new();
        public Entry OrderEntry { get; } = new() { Keyboard = Keyboard.Numeric, Placeholder = "0" };
        public Editor DropdownValuesEditor { get; } = new()
        {
            AutoSize = EditorAutoSizeOption.TextChanges,
            HeightRequest = 80,
            Placeholder = "One dropdown value per line"
        };
        public Button SaveButton { get; } = new()
        {
            Text = "Save Field",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            CornerRadius = 10
        };
        public Button DeactivateButton { get; } = new()
        {
            Text = "Deactivate",
            BackgroundColor = Colors.DarkRed,
            TextColor = Colors.White,
            CornerRadius = 10
        };

        private readonly VerticalStackLayout _dropdownSection;

        public FieldEditor(UdmdConfigModel config)
        {
            Config = config;

            NameEntry.Text = config.FieldName;
            TypePicker.SelectedItem = config.FieldTypeKind.ToString();
            RequiredSwitch.IsToggled = config.IsRequired;
            ActiveSwitch.IsToggled = config.IsActive;
            OrderEntry.Text = config.DisplayOrder.ToString(CultureInfo.InvariantCulture);

            _dropdownSection = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = "Dropdown Values", FontAttributes = FontAttributes.Bold },
                    DropdownValuesEditor
                }
            };

            var requiredRow = CreateSwitchRow("Required", RequiredSwitch);
            var activeRow = CreateSwitchRow("Active", ActiveSwitch);

            var buttons = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10,
                Children = { DeactivateButton, SaveButton }
            };
            Grid.SetColumn(DeactivateButton, 0);
            Grid.SetColumn(SaveButton, 1);

            var layout = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Field Name", FontAttributes = FontAttributes.Bold },
                    NameEntry,
                    new Label { Text = "Field Type", FontAttributes = FontAttributes.Bold },
                    TypePicker,
                    requiredRow,
                    new Label { Text = "Display Order", FontAttributes = FontAttributes.Bold },
                    OrderEntry,
                    activeRow,
                    _dropdownSection,
                    buttons
                }
            };

            Frame = new Frame
            {
                Padding = 12,
                CornerRadius = 10,
                HasShadow = true,
                Content = layout
            };

            UpdateDropdownVisibility();
        }

        public UdmdConfigModel ReadConfig(long cardId)
        {
            var fieldTypeText = TypePicker.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(fieldTypeText))
                fieldTypeText = UdmdFieldType.Text.ToString();

            if (!int.TryParse(OrderEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
                order = 0;

            Config.CardID = cardId;
            Config.FieldName = (NameEntry.Text ?? "").Trim();
            Config.FieldType = fieldTypeText;
            Config.IsRequired = RequiredSwitch.IsToggled;
            Config.DisplayOrder = order;
            Config.IsActive = ActiveSwitch.IsToggled;

            return Config;
        }

        public void UpdateDropdownVisibility()
        {
            _dropdownSection.IsVisible = string.Equals(
                TypePicker.SelectedItem as string,
                UdmdFieldType.Dropdown.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static Grid CreateSwitchRow(string label, Switch toggle)
        {
            var labelView = new Label
            {
                Text = label,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            row.Children.Add(labelView);
            row.Children.Add(toggle);
            Grid.SetColumn(toggle, 1);

            return row;
        }
    }
}
