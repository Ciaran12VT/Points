using System.Globalization;
using System.Text.Json;
using Points.Models;
using Points.Models.Watch;
using Points.Services.Locks;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services.Watch;

public sealed class WatchCommandProcessor : IWatchCommandProcessor
{
    private readonly IWatchEventStore _events;
    private readonly IWatchShortcutSettingsService _watchShortcuts;
    private readonly IWatchSnapshotPublishService _snapshots;
    private readonly ICardReadService _cards;
    private readonly IActivityService _activity;
    private readonly IBudgetService _budgets;
    private readonly IScCardService _scCards;
    private readonly IUdmdService _udmd;
    private readonly IActiveCardNotificationService _activeCardNotifications;
    private readonly IActiveCardChangeNotifier _activeCardChanges;
    private readonly IClock _clock;

    public WatchCommandProcessor(
        IWatchEventStore events,
        IWatchShortcutSettingsService watchShortcuts,
        IWatchSnapshotPublishService snapshots,
        ICardReadService cards,
        IActivityService activity,
        IBudgetService budgets,
        IScCardService scCards,
        IUdmdService udmd,
        IActiveCardNotificationService activeCardNotifications,
        IActiveCardChangeNotifier activeCardChanges,
        IClock clock)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _watchShortcuts = watchShortcuts ?? throw new ArgumentNullException(nameof(watchShortcuts));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        _scCards = scCards ?? throw new ArgumentNullException(nameof(scCards));
        _udmd = udmd ?? throw new ArgumentNullException(nameof(udmd));
        _activeCardNotifications = activeCardNotifications ?? throw new ArgumentNullException(nameof(activeCardNotifications));
        _activeCardChanges = activeCardChanges ?? throw new ArgumentNullException(nameof(activeCardChanges));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<WatchCommandResult> ProcessCommandJsonAsync(string commandJson, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        WatchCommandEvent? command;
        try
        {
            command = JsonSerializer.Deserialize<WatchCommandEvent>(commandJson, WatchJson.Options);
        }
        catch (JsonException ex)
        {
            return WatchCommandResult.Rejected($"Invalid command JSON: {ex.Message}");
        }

        if (command == null)
            return WatchCommandResult.Rejected("Command payload was empty.");

        if (command.SchemaVersion != WatchConstants.SchemaVersion)
            return WatchCommandResult.Rejected($"Unsupported schema version {command.SchemaVersion}.");

        if (string.IsNullOrWhiteSpace(command.EventId))
            return WatchCommandResult.Rejected("Missing eventId.");

        var began = await _events.TryBeginProcessingAsync(
            command.EventId,
            command.BaseSnapshotId,
            command.CreatedAtUtc,
            ct);

        if (!began)
            return WatchCommandResult.IgnoredDuplicate();

        WatchCommandResult result;
        try
        {
            result = await ProcessCommandAsync(command, ct);
        }
        catch (Exception ex)
        {
            result = WatchCommandResult.Rejected(ex.Message);
        }

        await _events.MarkProcessedAsync(
            command.EventId,
            result.Accepted ? "Accepted" : "Rejected",
            result.Message,
            ct);

        await _snapshots.RequestPublishAsync(force: true, ct);
        return result;
    }

    private async Task<WatchCommandResult> ProcessCommandAsync(WatchCommandEvent command, CancellationToken ct)
    {
        if (!WatchConstants.TryParseWatchCardId(command.CardId, out var phoneCardId))
            return WatchCommandResult.Rejected("Invalid cardId.");

        return command.ActionName switch
        {
            WatchConstants.ToggleActiveAction => await ProcessToggleActiveAsync(command, phoneCardId, ct),
            WatchConstants.RecordSpendAction => await ProcessRecordSpendAsync(command, phoneCardId, ct),
            WatchConstants.CommitStepRepsAction => await ProcessCommitStepRepsAsync(command, phoneCardId, ct),
            _ => WatchCommandResult.Rejected($"Unsupported action '{command.ActionName}'.")
        };
    }

    private async Task<WatchCommandResult> ProcessToggleActiveAsync(
        WatchCommandEvent command,
        long phoneCardId,
        CancellationToken ct)
    {
        if (!TryGetBool(command.Payload, "isActive", out var requestedActive))
            return WatchCommandResult.Rejected("toggleActive requires boolean payload.isActive.");

        var current = await _activity.GetCurrentActiveActivityAsync();
        var nowUtc = _clock.UtcNow;

        if (!requestedActive)
        {
            if (current == null)
            {
                PublishActiveCardChanged(null);
                return WatchCommandResult.Success("No active card to stop.");
            }

            if (current.CardID != phoneCardId)
                return WatchCommandResult.Rejected("Cannot stop a card that is not currently active.");

            var stopResult = await _activity.ToggleActivityAsync(phoneCardId, nowUtc, "Base Rate", 0);
            PublishActiveCardChanged(null, stopResult);
            return WatchCommandResult.Success("Active card stopped.");
        }

        if (current?.CardID == phoneCardId)
            return WatchCommandResult.Success("Card is already active.");

        var snapshotData = await LoadDailySeedAsync();
        var card = snapshotData.MainQuestCards.FirstOrDefault(c => c.CardID == phoneCardId);
        if (card == null)
            return WatchCommandResult.Rejected("Only TatCard and ScCard shortcuts can be started from the watch.");

        if (!await IsSelectedEligibleCardAsync(phoneCardId, "tat", "sc"))
            return WatchCommandResult.Rejected("Card is not currently eligible for watch activation.");

        if (await HasRequiredMetadataAsync(phoneCardId))
            return WatchCommandResult.Rejected("Card requires metadata and cannot be started from the watch.");

        var activeCards = snapshotData.MainQuestCards.Concat(snapshotData.MissionCards).ToList();
        if (LockEvaluator.IsLockedNow(card, _clock.LocalNow, activeCards, out _))
            return WatchCommandResult.Rejected("Card is locked.");

        var startResult = await _activity.ToggleActivityAsync(phoneCardId, nowUtc, "Base Rate", card.ValuePerMinute);
        if (startResult.Opened != null)
        {
            ApplyOpenedActivity(card, startResult.Opened);
            PublishActiveCardChanged(card, startResult);
        }

        return WatchCommandResult.Success("Card activated.");
    }

    private async Task<WatchCommandResult> ProcessRecordSpendAsync(
        WatchCommandEvent command,
        long phoneCardId,
        CancellationToken ct)
    {
        if (!TryGetDouble(command.Payload, "amount", out var amount) || amount <= 0)
            return WatchCommandResult.Rejected("recordSpend requires a positive numeric amount.");

        if (!await IsSelectedEligibleCardAsync(phoneCardId, "budget"))
            return WatchCommandResult.Rejected("Budget card is not currently eligible on the watch.");

        if (await HasRequiredMetadataAsync(phoneCardId))
            return WatchCommandResult.Rejected("Budget card requires metadata and cannot receive watch transactions.");

        var seed = await LoadDailySeedAsync();
        var budget = seed.BudgetCards.OfType<BudgetCardModel>().FirstOrDefault(c => c.CardID == phoneCardId);
        if (budget == null)
            return WatchCommandResult.Rejected("Budget card was not found.");

        budget.Transactions.Add(new BudgetTransaction
        {
            Timestamp = _clock.UtcNow,
            Type = BudgetTransactionType.Spend,
            CurrencyAmount = amount,
            GlobalValueAmount = 0
        });

        await _budgets.SaveBudgetCardModelDataAsync(budget, budget.CardID);
        _activeCardChanges.NotifyCardDataChanged(phoneCardId, WatchConstants.RecordSpendAction);
        return WatchCommandResult.Success("Spend recorded.");
    }

    private async Task<WatchCommandResult> ProcessCommitStepRepsAsync(
        WatchCommandEvent command,
        long phoneCardId,
        CancellationToken ct)
    {
        if (!await IsSelectedEligibleCardAsync(phoneCardId, "sc"))
            return WatchCommandResult.Rejected("SC card is not currently eligible on the watch.");

        if (await HasRequiredMetadataAsync(phoneCardId))
            return WatchCommandResult.Rejected("SC card requires metadata and cannot receive watch reps.");

        var seed = await LoadDailySeedAsync();
        var sc = seed.MainQuestCards.OfType<ScCardModel>().FirstOrDefault(c => c.CardID == phoneCardId);
        if (sc == null)
            return WatchCommandResult.Rejected("SC card was not found.");

        var updates = ParseStepRepPayload(command.Payload);
        if (updates.Count == 0)
            return WatchCommandResult.Rejected("commitStepReps did not include any rep counts.");

        var stepsByWatchId = sc.Steps.ToDictionary(s => WatchConstants.ToWatchStepId(s.Id), StringComparer.Ordinal);
        var totalAdded = 0;
        var totalRemoved = 0;
        var timestamp = _clock.UtcNow;
        var timestampOffset = 0;

        foreach (var update in updates)
        {
            ct.ThrowIfCancellationRequested();

            if (!stepsByWatchId.TryGetValue(update.Key, out var step))
                return WatchCommandResult.Rejected($"Unknown stepId '{update.Key}'.");

            var currentCount = step.Reps.Count;
            var desiredCount = update.Value;
            var delta = desiredCount - currentCount;

            if (delta > 0)
            {
                for (var i = 0; i < delta; i++)
                {
                    await _activity.AddRepForStep(step.Id, timestamp.AddMilliseconds(timestampOffset++), step.StepValue);
                    totalAdded++;
                }
            }
            else if (delta < 0)
            {
                for (var i = 0; i < Math.Abs(delta); i++)
                {
                    await _scCards.RemoveRepForStepAsync(step.Id, timestamp);
                    totalRemoved++;
                }
            }
        }

        _activeCardChanges.NotifyCardDataChanged(phoneCardId, WatchConstants.CommitStepRepsAction);
        return WatchCommandResult.Success($"Step reps committed. Added {totalAdded}, removed {totalRemoved}.");
    }

    private async Task<HomeSeedData> LoadDailySeedAsync()
    {
        var now = _clock.LocalNow;
        var start = StrictTimeSerializer.RequireWallClockDateTime(now, nameof(now)).Date;
        var end = start.AddDays(1).AddTicks(-1);
        return await _cards.GetHomeSeedDataAsync(start, end);
    }

    private async Task<bool> IsSelectedEligibleCardAsync(long phoneCardId, params string[] allowedKinds)
    {
        var candidates = await _watchShortcuts.GetCandidatesAsync();
        return candidates.Any(candidate =>
            candidate.CardId == phoneCardId &&
            candidate.IsSelected &&
            allowedKinds.Contains(candidate.Kind, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<bool> HasRequiredMetadataAsync(long cardId)
    {
        var configs = await _udmd.GetActiveUdmdConfigsForCardAsync(cardId);
        return configs.Any(x => x.IsRequired);
    }

    private static Dictionary<string, int> ParseStepRepPayload(IReadOnlyDictionary<string, string> payload)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var kvp in payload)
        {
            if (!kvp.Key.StartsWith("repCount.", StringComparison.Ordinal))
                continue;

            var stepId = kvp.Key["repCount.".Length..];
            if (string.IsNullOrWhiteSpace(stepId))
                continue;

            if (!int.TryParse(kvp.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                throw new InvalidOperationException($"Invalid rep count for '{stepId}'.");

            if (count < 0)
                throw new InvalidOperationException($"Rep count for '{stepId}' cannot be negative.");

            result[stepId] = count;
        }

        return result;
    }

    private static bool TryGetBool(IReadOnlyDictionary<string, string> payload, string key, out bool value)
    {
        value = false;
        return payload.TryGetValue(key, out var raw) && bool.TryParse(raw, out value);
    }

    private static bool TryGetDouble(IReadOnlyDictionary<string, string> payload, string key, out double value)
    {
        value = 0;

        if (!payload.TryGetValue(key, out var raw))
            return false;

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private void PublishActiveCardChanged(
        IActiveCardModel? activeCard,
        ToggleActivityModelResult? toggleResult = null)
    {
        _activeCardNotifications.UpdateActiveCardNotification(activeCard);
        _activeCardChanges.NotifyActiveCardChanged(activeCard?.CardID, toggleResult);
    }

    private static void ApplyOpenedActivity(IActiveCardModel card, ActivityModel activity)
    {
        var existing = card.Activity.FirstOrDefault(a => a.Id == activity.Id);
        if (existing == null)
        {
            card.Activity.Add(activity);
        }
        else
        {
            existing.CardID = activity.CardID;
            existing.StartDate = activity.StartDate;
            existing.EndDate = activity.EndDate;
            existing.RateName = activity.RateName;
            existing.ValuePerMinute = activity.ValuePerMinute;
        }

        card.IsActive = true;
    }
}
