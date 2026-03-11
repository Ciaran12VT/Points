using Points.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Points.ViewModels;

public enum TargetCardType
{
    MainQuest,
    Mission,
    Budget,
    Achievement,
    Arc,
    Planner
}

public sealed class CardOption
{
    public long CardId { get; set; }
    public string Title { get; set; } = "";
}

public sealed class ShortcutDetailsViewModel : ObservableObject
{
    private readonly ShortcutModel _model;
    private readonly Action<ShortcutModel> _onSaved;
    private readonly Action<ShortcutModel>? _onDelete;

    private readonly Dictionary<TargetCardType, List<CardOption>> _optionsByType;
    public ShortcutGroupModel? SelectedGroup { get; set; }
    public ObservableCollection<TargetCardType> TargetCardTypeOptions { get; } =
        new(Enum.GetValues(typeof(TargetCardType)).Cast<TargetCardType>());

    public ObservableCollection<CardOption> TargetCardOptions { get; } = new();

    // ---- Editable Fields ----

    private string _iconChar = "";
    public string IconChar { get => _iconChar; set => SetProperty(ref _iconChar, value); }

    private TargetCardType _selectedTargetCardType;
    public TargetCardType SelectedTargetCardType
    {
        get => _selectedTargetCardType;
        set
        {
            if (SetProperty(ref _selectedTargetCardType, value))
                RefreshTargetCards();
        }
    }

    private CardOption? _selectedTargetCard;
    public CardOption? SelectedTargetCard
    {
        get => _selectedTargetCard;
        set => SetProperty(ref _selectedTargetCard, value);
    }

    private string _shortcutOrderText = "0";
    public string ShortcutOrderText { get => _shortcutOrderText; set => SetProperty(ref _shortcutOrderText, value); }

    private string _groupName = "";
    public string GroupName 
    { 
        get => _groupName;
        set
        {
            SetProperty(ref _groupName, value);

            if(SelectedGroup != null)
            {
                GroupColor = SelectedGroup.Color;
                GroupOrderText = SelectedGroup.ShortcutGroupOrder.ToString(CultureInfo.InvariantCulture);
            }

        }
    }

    private string _groupOrderText = "0";
    public string GroupOrderText { get => _groupOrderText; set => SetProperty(ref _groupOrderText, value); }

    private Color _groupColor = Colors.Black;
    public Color GroupColor
    {
        get => _groupColor;
        set
        {
            if (SetProperty(ref _groupColor, value))
                RaisePropertyChanged(nameof(GroupColorHex));
        }
    }

    public string GroupColorHex => ToHexArgb(GroupColor);

    // ---- Error ----
    private string _errorText = "";
    public string ErrorText { get => _errorText; set { SetProperty(ref _errorText, value); RaisePropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    // ---- Commands ----
    public Command SaveCommand { get; }
    public Command CancelCommand { get; }
    public Command<string> SelectColorCommand { get; }

    public ShortcutDetailsViewModel(
        ShortcutModel model,
        Action<ShortcutModel> onSaved,
        Action<ShortcutModel>? onDelete,
        Dictionary<TargetCardType, List<CardOption>> optionsByType,
        // sensible defaults for new shortcuts:
        TargetCardType defaultType = TargetCardType.MainQuest)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _onSaved = onSaved ?? throw new ArgumentNullException(nameof(onSaved));
        _onDelete = onDelete;
        _optionsByType = optionsByType ?? new();

        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await OnCancelAsync());

        SelectColorCommand = new Command<string>(hex =>
        {
            GroupColor = Color.FromArgb(NormalizeArgbHex(hex));
        });

        // Seed from model (edit existing) or sensible defaults (new)
        IconChar = string.IsNullOrWhiteSpace(_model.IconChar) ? "" : _model.IconChar.Trim();

        // If editing and we can infer type from TargetCardId, HomeVM can pass in defaultType.
        SelectedTargetCardType = defaultType;

        ShortcutOrderText = _model.ShortcutOrder.ToString(CultureInfo.InvariantCulture);

        // Group info is best carried via ShortcutModel.Group (JOIN or previous edit)
        if (_model.Group != null)
        {
            GroupName = _model.Group.Name ?? "";
            GroupOrderText = _model.Group.ShortcutGroupOrder.ToString(CultureInfo.InvariantCulture);
            GroupColor = _model.Group.Color;
        }
        else
        {
            GroupName = "";
            GroupOrderText = "0";
            GroupColor = Colors.Black;
        }

        RefreshTargetCards();

        // Try select TargetCard based on stored TargetCardId if editing:
        if (_model.TargetCardId > 0)
        {
            var found = TargetCardOptions.FirstOrDefault(x => x.CardId == _model.TargetCardId);
            if (found != null)
                SelectedTargetCard = found;
        }
    }

    private void RefreshTargetCards()
    {
        TargetCardOptions.Clear();

        if (_optionsByType.TryGetValue(SelectedTargetCardType, out var list) && list != null)
        {
            foreach (var item in list.OrderBy(x => x.Title))
                TargetCardOptions.Add(item);
        }

        // If current selected doesn't belong to new set, clear
        if (SelectedTargetCard != null && !TargetCardOptions.Any(x => x.CardId == SelectedTargetCard.CardId))
            SelectedTargetCard = null;
    }

    private async Task SaveAsync()
    {
        ErrorText = "";

        // Icon validation
        var icon = (IconChar ?? "").Trim();
        if (icon.Length == 0)
        {
            ErrorText = "Icon is required.";
            return;
        }

        // Target validation
        if (SelectedTargetCard == null || SelectedTargetCard.CardId <= 0)
        {
            ErrorText = "Please select a target card.";
            return;
        }

        // Group validation
        var groupName = (GroupName ?? "").Trim();
        if (groupName.Length == 0)
        {
            ErrorText = "Group is required.";
            return;
        }

        // Parse orders
        if (!int.TryParse(ShortcutOrderText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortcutOrder))
            shortcutOrder = 0;

        if (!int.TryParse(GroupOrderText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupOrder))
            groupOrder = 0;

        // Apply back to domain model
        _model.IconChar = icon;
        _model.TargetCardId = SelectedTargetCard.CardId;
        _model.ShortcutOrder = shortcutOrder;

        // Carry group info back to HomeVM (HomeVM will upsert group, then set FK id on shortcut)
        _model.Group ??= new ShortcutGroupModel();
        _model.Group.Name = groupName;
        _model.Group.Color = GroupColor;
        _model.Group.ShortcutGroupOrder = groupOrder;

        _onSaved(_model);

        await Shell.Current.Navigation.PopAsync();
    }

    private async Task OnCancelAsync()
    {
        // Match your TatDetails pattern: action sheet with Delete option.
        var choice = await Shell.Current.DisplayActionSheet(
            "Shortcut",
            "Cancel",
            null,
            "Delete"
        );

        if (choice == "Delete")
        {
            _onDelete?.Invoke(_model);
            await Shell.Current.Navigation.PopAsync();
        }
    }

    // Color helpers (same as DB helper idea)
    private static string ToHexArgb(Color c)
    {
        byte a = (byte)Math.Round(c.Alpha * 255);
        byte r = (byte)Math.Round(c.Red * 255);
        byte g = (byte)Math.Round(c.Green * 255);
        byte b = (byte)Math.Round(c.Blue * 255);
        return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }

    private static string NormalizeArgbHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return "#FF000000";

        hex = hex.Trim();
        if (!hex.StartsWith("#"))
            hex = "#" + hex;

        // #RRGGBB -> #FFRRGGBB
        if (hex.Length == 7)
            return "#FF" + hex.Substring(1);

        if (hex.Length == 9)
            return hex;

        return "#FF000000";
    }
}