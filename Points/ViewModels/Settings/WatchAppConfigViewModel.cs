using System.Collections.ObjectModel;
using System.Windows.Input;
using Points.Models;
using Points.Models.Watch;
using Points.Services.Diagnostics;
using Points.Services.Watch;

namespace Points.ViewModels.Settings;

public sealed class WatchAppConfigViewModel : ObservableObject
{
    private readonly IWatchShortcutSettingsService _watchShortcuts;
    private readonly IWatchSnapshotPublishService _watchSnapshots;
    private readonly Func<Task>? _onSaved;

    private string _statusText = "";
    private bool _isBusy;

    public WatchAppConfigViewModel(
        IWatchShortcutSettingsService watchShortcuts,
        IWatchSnapshotPublishService watchSnapshots,
        Func<Task>? onSaved = null)
    {
        _watchShortcuts = watchShortcuts ?? throw new ArgumentNullException(nameof(watchShortcuts));
        _watchSnapshots = watchSnapshots ?? throw new ArgumentNullException(nameof(watchSnapshots));
        _onSaved = onSaved;

        SaveCommand = new Command(async () => await SaveAsync(), () => !IsBusy);
        RefreshCommand = new Command(async () => await LoadAsync(), () => !IsBusy);

        LoadAsync().Forget("Load watch app config");
    }

    public ObservableCollection<WatchShortcutOptionViewModel> Shortcuts { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand RefreshCommand { get; }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, value))
                RaisePropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                (SaveCommand as Command)?.ChangeCanExecute();
                (RefreshCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public string SelectedCountText => $"{SelectedCount}/{WatchConstants.MaxShortcutCount} selected";

    public int SelectedCount => Shortcuts.Count(x => x.IsSelected);

    public bool HasNoEligibleCards => !IsBusy && Shortcuts.Count == 0;

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusText = "";

        try
        {
            var candidates = await _watchShortcuts.GetCandidatesAsync();

            Shortcuts.Clear();
            foreach (var candidate in candidates)
            {
                Shortcuts.Add(new WatchShortcutOptionViewModel(candidate, OnShortcutSelectionChanged));
            }

            RefreshCounts();
        }
        finally
        {
            IsBusy = false;
            RaisePropertyChanged(nameof(HasNoEligibleCards));
        }
    }

    private void OnShortcutSelectionChanged(WatchShortcutOptionViewModel option, bool requestedValue)
    {
        if (requestedValue && SelectedCount > WatchConstants.MaxShortcutCount)
        {
            option.SetSelectedSilently(false);
            StatusText = $"Only {WatchConstants.MaxShortcutCount} watch shortcuts can be selected.";
        }
        else
        {
            StatusText = "";
        }

        RefreshCounts();
    }

    private async Task SaveAsync()
    {
        if (SelectedCount > WatchConstants.MaxShortcutCount)
        {
            StatusText = $"Only {WatchConstants.MaxShortcutCount} watch shortcuts can be selected.";
            return;
        }

        IsBusy = true;

        try
        {
            var selectedIds = Shortcuts
                .Where(x => x.IsSelected)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => x.CardId)
                .ToList();

            await _watchShortcuts.SaveSelectedCardIdsAsync(selectedIds);
            await _watchSnapshots.RequestPublishAsync(force: true);

            StatusText = "Watch shortcuts saved.";
            if (_onSaved != null)
                await _onSaved();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCounts()
    {
        RaisePropertyChanged(nameof(SelectedCount));
        RaisePropertyChanged(nameof(SelectedCountText));
        RaisePropertyChanged(nameof(HasNoEligibleCards));
    }
}

public sealed class WatchShortcutOptionViewModel : ObservableObject
{
    private readonly Action<WatchShortcutOptionViewModel, bool> _onSelectionChanged;
    private bool _isSelected;
    private bool _suppressCallback;

    public WatchShortcutOptionViewModel(
        WatchShortcutCandidate candidate,
        Action<WatchShortcutOptionViewModel, bool> onSelectionChanged)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));

        _onSelectionChanged = onSelectionChanged ?? throw new ArgumentNullException(nameof(onSelectionChanged));

        CardId = candidate.CardId;
        WatchCardId = candidate.WatchCardId;
        Title = candidate.Title;
        Kind = candidate.Kind;
        IconChar = candidate.IconChar;
        DisplayOrder = candidate.DisplayOrder;
        _isSelected = candidate.IsSelected;
    }

    public long CardId { get; }
    public string WatchCardId { get; }
    public string Title { get; }
    public string Kind { get; }
    public string IconChar { get; }
    public int DisplayOrder { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
                return;

            if (!_suppressCallback)
                _onSelectionChanged(this, value);
        }
    }

    public string KindText => Kind switch
    {
        "tat" => "TatCard",
        "sc" => "ScCard",
        "budget" => "BudgetCard",
        _ => Kind
    };

    public void SetSelectedSilently(bool value)
    {
        _suppressCallback = true;
        try
        {
            IsSelected = value;
        }
        finally
        {
            _suppressCallback = false;
        }
    }
}
