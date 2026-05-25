using System.Text.Json;
using Points.Global;
using Points.Models;
using Points.Models.Watch;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Watch;

public sealed class WatchShortcutSettingsService : IWatchShortcutSettingsService
{
    private readonly ISettingsService _settings;
    private readonly IShortcutService _shortcuts;
    private readonly ICardReadService _cards;
    private readonly IUdmdService _udmd;
    private readonly IClock _clock;

    public WatchShortcutSettingsService(
        ISettingsService settings,
        IShortcutService shortcuts,
        ICardReadService cards,
        IUdmdService udmd,
        IClock clock)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<IReadOnlyList<WatchShortcutCandidate>> GetCandidatesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var selected = (await GetSelectedCardIdsAsync(ct)).ToHashSet();
        var shortcuts = await _shortcuts.GetDashboardShortcutsAsync();
        var orderedShortcuts = shortcuts
            .Where(s => s.TargetCardId > 0)
            .GroupBy(s => s.TargetCardId)
            .Select(g => g.OrderBy(s => s.Group?.ShortcutGroupOrder ?? 0)
                .ThenBy(s => s.ShortcutOrder)
                .ThenBy(s => s.ShortcutId)
                .First())
            .OrderBy(s => s.Group?.ShortcutGroupOrder ?? 0)
            .ThenBy(s => s.ShortcutOrder)
            .ThenBy(s => s.ShortcutId)
            .ToList();

        var now = _clock.LocalNow;
        var todayStart = StrictTimeSerializer.RequireWallClockDateTime(now, nameof(now)).Date;
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);
        var seed = await _cards.GetHomeSeedDataAsync(todayStart, todayEnd);

        var cardMap = seed.MainQuestCards
            .Cast<ICardModel>()
            .Concat(seed.BudgetCards)
            .Where(IsWatchShortcutKind)
            .GroupBy(c => c.CardID)
            .ToDictionary(g => g.Key, g => g.First());

        var candidates = new List<WatchShortcutCandidate>();
        var displayOrder = 0;

        foreach (var shortcut in orderedShortcuts)
        {
            ct.ThrowIfCancellationRequested();

            if (!cardMap.TryGetValue(shortcut.TargetCardId, out var card))
                continue;

            if (await HasRequiredMetadataAsync(card.CardID))
                continue;

            candidates.Add(new WatchShortcutCandidate
            {
                CardId = card.CardID,
                WatchCardId = WatchConstants.ToWatchCardId(card.CardID),
                Title = card.Title,
                Kind = GetWatchKind(card),
                IconChar = shortcut.IconChar,
                DisplayOrder = ++displayOrder,
                IsSelected = selected.Contains(card.CardID)
            });
        }

        return candidates;
    }

    public async Task<IReadOnlyList<long>> GetSelectedCardIdsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var settings = await _settings.GetSettingsAsync();
        var raw = settings.FirstOrDefault(x => x.SettingKey == SettingKeys.WatchShortcutCardIds)?.StringValue
            ?? settings.FirstOrDefault(x => x.SettingKey == SettingKeys.WatchShortcutCardIds)?.RawValue
            ?? "";

        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<long>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<long>>(raw, WatchJson.Options);
            if (parsed == null)
                return Array.Empty<long>();

            return parsed
                .Where(id => id > 0)
                .Distinct()
                .Take(WatchConstants.MaxShortcutCount)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<long>();
        }
    }

    public async Task SaveSelectedCardIdsAsync(IReadOnlyList<long> cardIds, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var clean = (cardIds ?? Array.Empty<long>())
            .Where(id => id > 0)
            .Distinct()
            .Take(WatchConstants.MaxShortcutCount)
            .ToList();

        var json = JsonSerializer.Serialize(clean, WatchJson.Options);
        await _settings.SetStringSettingAsync(SettingKeys.WatchShortcutCardIds, json);
        SettingsProvider.UpdateString(SettingKeys.WatchShortcutCardIds, json);
    }

    private async Task<bool> HasRequiredMetadataAsync(long cardId)
    {
        var configs = await _udmd.GetActiveUdmdConfigsForCardAsync(cardId);
        return configs.Any(x => x.IsRequired);
    }

    private static bool IsWatchShortcutKind(ICardModel card) =>
        card is TatCardModel or ScCardModel or BudgetCardModel;

    private static string GetWatchKind(ICardModel card) =>
        card switch
        {
            ScCardModel => "sc",
            TatCardModel => "tat",
            BudgetCardModel => "budget",
            _ => "unknown"
        };
}
