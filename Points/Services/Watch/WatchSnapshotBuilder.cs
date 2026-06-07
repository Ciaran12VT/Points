using System.Globalization;
using System.Text.Json;
using Points.Global;
using Points.Models;
using Points.Models.Watch;
using Points.Services.Locks;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Watch;

public sealed class WatchSnapshotBuilder : IWatchSnapshotBuilder
{
    private readonly ICardReadService _cards;
    private readonly IShortcutService _shortcuts;
    private readonly IWatchShortcutSettingsService _watchShortcuts;
    private readonly IActivityService _activity;
    private readonly IHardModePenaltyService _hardModePenalties;
    private readonly IClock _clock;
    private readonly ITimeZoneService _timeZoneService;

    public WatchSnapshotBuilder(
        ICardReadService cards,
        IShortcutService shortcuts,
        IWatchShortcutSettingsService watchShortcuts,
        IActivityService activity,
        IHardModePenaltyService hardModePenalties,
        IClock clock,
        ITimeZoneService timeZoneService)
    {
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));
        _watchShortcuts = watchShortcuts ?? throw new ArgumentNullException(nameof(watchShortcuts));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _hardModePenalties = hardModePenalties ?? throw new ArgumentNullException(nameof(hardModePenalties));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
    }

    public async Task<WatchSummarySnapshot> BuildSnapshotAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var nowLocal = _clock.LocalNow;
        var nowUtc = _clock.UtcNow;
        var todayStart = LocalDayStart(nowLocal);
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);

        var selectedIds = (await _watchShortcuts.GetCandidatesAsync(ct))
            .Where(candidate => candidate.IsSelected)
            .Select(candidate => candidate.CardId)
            .Take(WatchConstants.MaxShortcutCount)
            .ToList();

        var shortcutIcons = await GetShortcutIconsByCardIdAsync();
        var dailySeed = await _cards.GetHomeSeedDataAsync(todayStart, todayEnd);
        var globalSeed = await _cards.GetHomeSeedDataAsync(GlobalVariables.RangeStart, GlobalVariables.RangeEnd);
        var openActivity = await _activity.GetCurrentActiveActivityAsync();

        var activeCardsForLocks = dailySeed.MainQuestCards
            .Concat(dailySeed.MissionCards)
            .ToList();

        var activeCardsById = activeCardsForLocks
            .GroupBy(c => c.CardID)
            .ToDictionary(g => g.Key, g => g.First());

        var selectedSet = selectedIds.ToHashSet();

        var selectedActiveCards = dailySeed.MainQuestCards
            .Where(card => selectedSet.Contains(card.CardID))
            .OrderBy(card => selectedIds.IndexOf(card.CardID))
            .ToList();

        var selectedBudgetCards = dailySeed.BudgetCards
            .OfType<BudgetCardModel>()
            .Where(card => selectedSet.Contains(card.CardID))
            .OrderBy(card => selectedIds.IndexOf(card.CardID))
            .ToList();

        var fullWatchCardIds = selectedActiveCards
            .Cast<ICardModel>()
            .Concat(selectedBudgetCards)
            .Select(card => WatchConstants.ToWatchCardId(card.CardID))
            .ToHashSet(StringComparer.Ordinal);

        var activeCard = BuildActiveCard(openActivity, activeCardsById, fullWatchCardIds);
        var cards = selectedActiveCards
            .Select((card, index) => BuildActiveCardSummary(
                card,
                index + 1,
                shortcutIcons.GetValueOrDefault(card.CardID) ?? "",
                openActivity,
                nowLocal,
                nowUtc,
                activeCardsForLocks))
            .ToList();

        var budgets = selectedBudgetCards
            .Select((card, index) => BuildBudgetCardSummary(
                card,
                index + 1,
                shortcutIcons.GetValueOrDefault(card.CardID) ?? "",
                nowLocal))
            .ToList();

        var globalScore = await CalculateGlobalScoreAsync(globalSeed, nowUtc);
        var radialIds = selectedIds
            .Select(WatchConstants.ToWatchCardId)
            .Where(id => cards.Any(c => c.CardId == id) || budgets.Any(b => b.CardId == id))
            .Take(WatchConstants.MaxShortcutCount)
            .ToList();

        return new WatchSummarySnapshot
        {
            SchemaVersion = WatchConstants.SchemaVersion,
            SnapshotId = $"watch-snapshot-{Guid.NewGuid():N}",
            GeneratedAtUtc = StrictTimeSerializer.SerializeUtcInstant(nowUtc),
            LocalNow = FormatLocalWithOffset(nowLocal),
            Timezone = _timeZoneService.LocalTimeZone.Id,
            Global = new WatchGlobalSummary
            {
                Score = Round(globalScore, 2),
                DisplayText = Round(globalScore, 2).ToString("0.##", CultureInfo.InvariantCulture),
                Tone = ToneFromValue(globalScore),
                ActiveCard = activeCard,
                Notification = null
            },
            WatchNavigation = new WatchNavigationSummary
            {
                SelectedCardId = radialIds.FirstOrDefault(),
                RadialMenuCardIds = radialIds
            },
            Cards = cards,
            BudgetCards = budgets
        };
    }

    public async Task<string> BuildSnapshotJsonAsync(CancellationToken ct = default)
    {
        var snapshot = await BuildSnapshotAsync(ct);
        return JsonSerializer.Serialize(snapshot, WatchJson.Options);
    }

    private async Task<Dictionary<long, string>> GetShortcutIconsByCardIdAsync()
    {
        var shortcuts = await _shortcuts.GetDashboardShortcutsAsync();

        return shortcuts
            .Where(s => s.TargetCardId > 0)
            .GroupBy(s => s.TargetCardId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.Group?.ShortcutGroupOrder ?? 0)
                    .ThenBy(s => s.ShortcutOrder)
                    .ThenBy(s => s.ShortcutId)
                    .First()
                    .IconChar ?? "");
    }

    private WatchActiveCardSummary? BuildActiveCard(
        ActivityModel? openActivity,
        IReadOnlyDictionary<long, IActiveCardModel> activeCardsById,
        IReadOnlySet<string> fullWatchCardIds)
    {
        if (openActivity == null)
            return null;

        if (!activeCardsById.TryGetValue(openActivity.CardID, out var card))
            return new WatchActiveCardSummary
            {
                CardId = WatchConstants.ToWatchCardId(openActivity.CardID),
                PhoneCardId = openActivity.CardID,
                Kind = "unknown",
                Title = $"Card {openActivity.CardID}",
                Tone = "neutral",
                ManageableOnWatch = false
            };

        var watchCardId = WatchConstants.ToWatchCardId(card.CardID);
        return new WatchActiveCardSummary
        {
            CardId = watchCardId,
            PhoneCardId = card.CardID,
            Kind = GetWatchKind(card),
            Title = card.Title,
            Tone = ToneFromValue(card.ValuePerMinute),
            ManageableOnWatch = fullWatchCardIds.Contains(watchCardId) && card is TatCardModel or ScCardModel
        };
    }

    private WatchCardSummary BuildActiveCardSummary(
        IActiveCardModel card,
        int displayOrder,
        string icon,
        ActivityModel? openActivity,
        DateTime nowLocal,
        DateTime nowUtc,
        IReadOnlyList<IActiveCardModel> activeCardsForLocks)
    {
        var todayStart = LocalDayStart(nowLocal);
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);
        var points = MultiplierValueCalculator.GetValue(card, todayStart, todayEnd);
        var isActive = openActivity?.CardID == card.CardID;
        var isLocked = LockEvaluator.IsLockedNow(card, nowLocal, activeCardsForLocks, out _);

        var summary = new WatchCardSummary
        {
            CardId = WatchConstants.ToWatchCardId(card.CardID),
            PhoneCardId = card.CardID,
            Kind = GetWatchKind(card),
            Title = card.Title,
            IconKey = icon,
            DisplayOrder = displayOrder,
            IsActive = isActive,
            IsLocked = isLocked,
            Tone = ToneFromValue(card.ValuePerMinute),
            CurrentValue = new WatchValueSummary
            {
                Points = Round(points, 1),
                DisplayText = $"{Round(points, 1):0.#} pts"
            },
            ActiveSession = isActive && openActivity != null
                ? BuildActiveSession(openActivity, nowUtc)
                : null,
            SupportedActions = card is ScCardModel
                ? new List<string> { WatchConstants.ToggleActiveAction, WatchConstants.CommitStepRepsAction }
                : new List<string> { WatchConstants.ToggleActiveAction }
        };

        if (card is ScCardModel sc)
        {
            summary.Steps = sc.Steps
                .OrderBy(step => step.SortOrder)
                .Select(step => new WatchStepSummary
                {
                    StepId = WatchConstants.ToWatchStepId(step.Id),
                    PhoneStepId = step.Id,
                    Title = step.Title,
                    RepCount = step.Reps.Count,
                    StepValue = step.StepValue,
                    CanIncrement = true,
                    CanDecrement = step.Reps.Count > 0
                })
                .ToList();
        }

        return summary;
    }

    private WatchBudgetCardSummary BuildBudgetCardSummary(
        BudgetCardModel budget,
        int displayOrder,
        string icon,
        DateTime nowLocal)
    {
        budget.NotifyTimeChanged(nowLocal);

        var balance = budget.GetBalance(nowLocal);
        var dailyTopUp = budget.GetDailyTopUpTotal(nowLocal.Date);
        var percent = dailyTopUp <= 0 ? 0 : balance / dailyTopUp * 100;
        var nextTopUp = budget.GetNextTopUp(nowLocal);

        return new WatchBudgetCardSummary
        {
            CardId = WatchConstants.ToWatchCardId(budget.CardID),
            PhoneCardId = budget.CardID,
            Kind = "budget",
            Title = budget.Title,
            IconKey = icon,
            DisplayOrder = displayOrder,
            Currency = budget.Currency,
            Balance = Round(balance, 1),
            BalanceDisplayText = $"{Round(balance, 1):0.#} {budget.Currency}",
            PercentRemaining = Round(percent, 1),
            PercentDisplayText = $"{Round(percent, 0):0}%",
            Tone = percent <= 25 ? "negative" : "positive",
            ExchangeRate = budget.ExchangeRate,
            CashInEnabled = SettingsProvider.IsCashInEnabled,
            NextTopUp = nextTopUp.HasValue
                ? new WatchTopUpSummary
                {
                    AtLocal = FormatLocalWithOffset(nextTopUp.Value.When),
                    Amount = nextTopUp.Value.Amount,
                    CountdownSeconds = Math.Max(0, (long)(nextTopUp.Value.When - nowLocal).TotalSeconds),
                    CountdownDisplayText = FormatTopUpCountdown(nextTopUp.Value.When - nowLocal)
                }
                : null,
            SupportedActions = new List<string> { WatchConstants.RecordSpendAction }
        };
    }

    private async Task<double> CalculateGlobalScoreAsync(HomeSeedData seed, DateTime utcNow)
    {
        var rangeStart = GlobalVariables.RangeStart;
        var rangeEnd = GlobalVariables.RangeEnd;

        var total = seed.MainQuestCards
            .Cast<ICardModel>()
            .Concat(seed.MissionCards)
            .Concat(seed.BudgetCards)
            .Concat(seed.Achievements)
            .Concat(seed.ValueTrackers)
            .Concat(seed.EventTrackers)
            .Sum(card => MultiplierValueCalculator.GetValue(card, rangeStart, rangeEnd));

        total += await _hardModePenalties.GetValueAsync(rangeStart, rangeEnd, utcNow);
        return total;
    }

    private static WatchActiveSessionSummary BuildActiveSession(ActivityModel activity, DateTime nowUtc)
    {
        var startUtc = StrictTimeSerializer.RequireUtcInstant(activity.StartDate, nameof(activity.StartDate));

        var elapsed = nowUtc - startUtc;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        return new WatchActiveSessionSummary
        {
            StartedAtUtc = StrictTimeSerializer.SerializeUtcInstant(startUtc),
            ElapsedSeconds = (long)elapsed.TotalSeconds,
            DisplayText = FormatDuration(elapsed),
            RateName = string.IsNullOrWhiteSpace(activity.RateName) ? "Base Rate" : activity.RateName,
            ValuePerMinute = activity.ValuePerMinute
        };
    }

    private static string GetWatchKind(ICardModel card) =>
        card switch
        {
            ScCardModel => "sc",
            TatCardModel => "tat",
            MissionCardModel => "mission",
            BudgetCardModel => "budget",
            _ => "unknown"
        };

    private static string ToneFromValue(double value)
    {
        if (value < 0)
            return "negative";

        return Math.Abs(value) < 0.0001 ? "neutral" : "positive";
    }

    private static double Round(double value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes} mins";

        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes} mins";

        return $"{Math.Max(0, duration.Seconds)} secs";
    }

    private static string FormatTopUpCountdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var hours = (int)remaining.TotalHours;
        return $"Next Top-up In: {hours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private string FormatLocalWithOffset(DateTime local)
    {
        local = StrictTimeSerializer.RequireWallClockDateTime(local, nameof(local));
        var offset = _timeZoneService.LocalTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);
    }

    private static DateTime LocalDayStart(DateTime local) =>
        StrictTimeSerializer.RequireWallClockDateTime(local, nameof(local)).Date;
}
