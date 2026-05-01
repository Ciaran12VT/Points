using System.Globalization;
using Points.Models;
using Points.Services.Navigation;
using Points.Services;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Views.Udmd;

public sealed class UdmdPromptResult
{
    public bool Cancelled { get; init; }
    public List<UdmdValueInput> Values { get; init; } = new();
    public List<string> CreatedImagePaths { get; init; } = new();

    public static UdmdPromptResult Empty { get; } = new();
    public static UdmdPromptResult CancelledResult { get; } = new() { Cancelled = true };

    public void CleanupCreatedImages()
    {
        foreach (var path in CreatedImagePaths)
            UdmdImageFileStore.TryDeleteFile(path);
    }
}

public sealed class UdmdPromptPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly long _cardId;
    private readonly IReadOnlyList<UdmdFieldPromptModel> _fields;
    private readonly TaskCompletionSource<UdmdPromptResult> _completion = new();
    private readonly Dictionary<long, View> _inputControls = new();
    private readonly List<string> _createdImagePaths = new();
    private readonly IClock _clock;
    private bool _completed;

    private UdmdPromptPage(
        long cardId,
        IReadOnlyList<UdmdFieldPromptModel> fields,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _clock = clock;
        _cardId = cardId;
        _fields = fields;
        Title = "Metadata";

        var content = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14
        };

        foreach (var field in _fields)
        {
            content.Children.Add(new Label
            {
                Text = field.Config.IsRequired ? $"{field.Config.FieldName} *" : field.Config.FieldName,
                FontAttributes = FontAttributes.Bold
            });

            var control = CreateInputControl(field);
            _inputControls[field.Config.UdmdConfigID] = control;
            content.Children.Add(control);
        }

        var cancel = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Gray,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            CornerRadius = 12
        };

        var save = new Button
        {
            Text = "Save",
            BackgroundColor = Colors.Green,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            CornerRadius = 12
        };

        cancel.Clicked += async (_, __) => await CancelAsync();
        save.Clicked += async (_, __) => await TrySaveAsync();

        var buttons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            Children = { cancel, save }
        };
        Grid.SetColumn(cancel, 0);
        Grid.SetColumn(save, 1);

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            Padding = 0,
            Children =
            {
                new ScrollView { Content = content },
                buttons
            }
        };
        Grid.SetRow(buttons, 1);
    }

    public static async Task<UdmdPromptResult> PromptForCardAsync(
        Page owner,
        IUdmdService udmd,
        long cardId,
        IClock clock,
        IAppNavigationService navigation,
        IAppDialogService dialogs)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        if (udmd == null)
            throw new ArgumentNullException(nameof(udmd));

        if (clock == null)
            throw new ArgumentNullException(nameof(clock));

        if (cardId <= 0)
            return UdmdPromptResult.Empty;

        if (navigation == null)
            throw new ArgumentNullException(nameof(navigation));

        if (dialogs == null)
            throw new ArgumentNullException(nameof(dialogs));

        var configs = await udmd.GetActiveUdmdConfigsForCardAsync(cardId);
        if (configs.Count == 0)
            return UdmdPromptResult.Empty;

        var fields = new List<UdmdFieldPromptModel>();
        foreach (var config in configs)
        {
            var dropdowns = config.FieldTypeKind == UdmdFieldType.Dropdown
                ? await udmd.GetDropdownValuesAsync(config.UdmdConfigID)
                : new List<UdmdDropdownModel>();

            fields.Add(new UdmdFieldPromptModel
            {
                Config = config,
                DropdownValues = dropdowns
            });
        }

        var page = new UdmdPromptPage(cardId, fields, navigation, dialogs, clock);
        await navigation.PushModalAsync(new NavigationPage(page));
        return await page._completion.Task;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (!_completed)
        {
            CleanupCreatedImages();
            _completion.TrySetResult(UdmdPromptResult.CancelledResult);
        }
    }

    private View CreateInputControl(UdmdFieldPromptModel field)
    {
        return field.Config.FieldTypeKind switch
        {
            UdmdFieldType.Dropdown => new Picker
            {
                ItemsSource = field.DropdownValues.Select(x => x.DropdownValue).ToList(),
                Title = "Choose value"
            },
            UdmdFieldType.Number => new Entry
            {
                Keyboard = Keyboard.Numeric,
                Placeholder = "Enter number"
            },
            UdmdFieldType.Date => new DatePicker
            {
                Date = _clock.LocalNow.Date
            },
            UdmdFieldType.Boolean => new Switch
            {
                IsToggled = false,
                HorizontalOptions = LayoutOptions.Start
            },
            UdmdFieldType.Image => CreateImageInputControl(field),
            _ => new Entry
            {
                Placeholder = "Enter text"
            }
        };
    }

    private ImageInputView CreateImageInputControl(UdmdFieldPromptModel field)
    {
        var control = new ImageInputView();
        control.CaptureButton.Clicked += async (_, __) => await CaptureImageAsync(field, control);
        return control;
    }

    private async Task CaptureImageAsync(UdmdFieldPromptModel field, ImageInputView control)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await _dialogs.DisplayAlertAsync("Camera unavailable", "This device does not support camera capture.", "OK");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = field.Config.FieldName
            });

            if (photo == null)
                return;

            var fileName = UdmdImageFileStore.CreateFileName(field.Config, _clock.LocalNow, photo.FileName);
            var destinationPath = UdmdImageFileStore.GetImagePath(_cardId, fileName);

            await using (var source = await photo.OpenReadAsync())
            await using (var destination = File.Create(destinationPath))
            {
                await source.CopyToAsync(destination);
            }

            if (!string.IsNullOrWhiteSpace(control.FilePath))
            {
                _createdImagePaths.Remove(control.FilePath);
                UdmdImageFileStore.TryDeleteFile(control.FilePath);
            }

            control.SetFile(fileName, destinationPath);
            _createdImagePaths.Add(destinationPath);
        }
        catch (TaskCanceledException)
        {
        }
        catch (PermissionException)
        {
            await _dialogs.DisplayAlertAsync("Camera permission", "Camera permission is required to capture this metadata image.", "OK");
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Image capture failed", ex.Message, "OK");
        }
    }

    private async Task TrySaveAsync()
    {
        var values = new List<UdmdValueInput>();

        foreach (var field in _fields)
        {
            var rawValue = ReadValue(field, _inputControls[field.Config.UdmdConfigID]);

            if (field.Config.IsRequired && string.IsNullOrWhiteSpace(rawValue))
            {
                await _dialogs.DisplayAlertAsync("Required field", $"{field.Config.FieldName} is required.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(rawValue))
                continue;

            if (field.Config.FieldTypeKind == UdmdFieldType.Number &&
                !double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                !double.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out _))
            {
                await _dialogs.DisplayAlertAsync("Invalid number", $"{field.Config.FieldName} must be a number.", "OK");
                return;
            }

            values.Add(new UdmdValueInput
            {
                UdmdConfigID = field.Config.UdmdConfigID,
                FieldValue = rawValue
            });
        }

        await CompleteAsync(new UdmdPromptResult
        {
            Values = values,
            CreatedImagePaths = _createdImagePaths.ToList()
        });
    }

    private static string? ReadValue(UdmdFieldPromptModel field, View control)
    {
        return control switch
        {
            Entry entry => (entry.Text ?? "").Trim(),
            Picker picker => picker.SelectedItem as string,
            DatePicker datePicker => datePicker.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Switch toggle => toggle.IsToggled ? "true" : "false",
            ImageInputView imageInput => imageInput.FileName,
            _ => null
        };
    }

    private async Task CancelAsync()
    {
        CleanupCreatedImages();
        await CompleteAsync(UdmdPromptResult.CancelledResult);
    }

    private async Task CompleteAsync(UdmdPromptResult result)
    {
        _completed = true;
        _completion.TrySetResult(result);
        await _navigation.PopModalAsync();
    }

    private void CleanupCreatedImages()
    {
        foreach (var path in _createdImagePaths.ToList())
            UdmdImageFileStore.TryDeleteFile(path);

        _createdImagePaths.Clear();
    }

    private sealed class ImageInputView : Grid
    {
        private readonly Label _fileNameLabel = new()
        {
            Text = "No image",
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        public Button CaptureButton { get; } = new()
        {
            Text = "Camera",
            BackgroundColor = Colors.DodgerBlue,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10,
            HeightRequest = 44
        };

        public string? FileName { get; private set; }
        public string? FilePath { get; private set; }

        public ImageInputView()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            ColumnSpacing = 10;

            Children.Add(CaptureButton);
            Children.Add(_fileNameLabel);
            Grid.SetColumn(_fileNameLabel, 1);
        }

        public void SetFile(string fileName, string filePath)
        {
            FileName = fileName;
            FilePath = filePath;
            _fileNameLabel.Text = fileName;
        }
    }
}
