using System.Globalization;
using Points.Global;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Views.Home;
using Points.Views.Udmd;

namespace Points.ViewModels.Home
{
    internal sealed class HomeValueEntryCoordinator
    {
        private readonly ICardWriteService _cardWriter;
        private readonly IBudgetService _budgets;
        private readonly ITrackerService _trackers;
        private readonly IUdmdService _udmd;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly IPopupService _popups;
        private readonly IAppPageService _pageService;
        private readonly IClock _clock;
        private readonly ITimeZoneService _timeZoneService;
        private readonly Func<DateTime> _getNow;

        public HomeValueEntryCoordinator(
            ICardWriteService cardWriter,
            IBudgetService budgets,
            ITrackerService trackers,
            IUdmdService udmd,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            IPopupService popups,
            IAppPageService pageService,
            IClock clock,
            ITimeZoneService timeZoneService,
            Func<DateTime> getNow)
        {
            _cardWriter = cardWriter;
            _budgets = budgets;
            _trackers = trackers;
            _udmd = udmd;
            _navigation = navigation;
            _dialogs = dialogs;
            _popups = popups;
            _pageService = pageService;
            _clock = clock;
            _timeZoneService = timeZoneService;
            _getNow = getNow;
        }

        public async Task AddTrackerValueWithMetadataAsync(TrackerCardModel? card)
        {
            await AddTrackerValueWithMetadataAsync(card, timestampUtc: null);
        }

        public async Task AddTrackerValueAtSelectedTimeAsync(TrackerCardModel? card)
        {
            if (card == null)
                return;

            var nowLocal = _clock.LocalNow;
            var minLocal = GetTrackerRegistrationLowerBoundLocal(card, nowLocal);
            var initialLocal = nowLocal.AddMinutes(-5);
            if (initialLocal < minLocal)
                initialLocal = minLocal;

            var chosenObj = await _popups.ShowPopupAsync(new EditActiveTimePopup(
                activationTypeText: "Recorded at",
                selectedTime: initialLocal,
                minTime: minLocal,
                maxTime: nowLocal,
                localNow: nowLocal));

            if (chosenObj is not DateTime chosenLocal)
                return;

            var chosenUtc = _timeZoneService.ToUtcFromLocal(chosenLocal);
            await AddTrackerValueWithMetadataAsync(card, chosenUtc);
        }

        private async Task AddTrackerValueWithMetadataAsync(TrackerCardModel? card, DateTime? timestampUtc)
        {
            if (card == null)
                return;

            if (timestampUtc.HasValue)
                timestampUtc = StrictTimeSerializer.RequireUtcInstant(timestampUtc.Value, nameof(timestampUtc));

            if (card is ValueTrackerCardModel valueCard)
            {
                var input = await _dialogs.DisplayPromptAsync(
                    "Add Value",
                    "Enter a value:",
                    accept: "OK",
                    cancel: "Cancel",
                    keyboard: Keyboard.Numeric);

                if (string.IsNullOrWhiteSpace(input))
                    return;

                if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                    !double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                {
                    await _dialogs.DisplayAlertAsync("Invalid value", "Please enter a valid number.", "OK");
                    return;
                }

                await AddTrackerValueAsync(valueCard, value, timestampUtc);
            }
            else if (card is EventTrackerCardModel eventCard)
            {
                await AddTrackerValueAsync(eventCard, 1, timestampUtc);
            }
        }

        public async Task PromptAndRecordBudgetTransactionAsync(BudgetCardModel? budget, BudgetTransactionType type)
        {
            if (budget == null)
                return;

            budget.IsCashInEnabled = SettingsProvider.IsCashInEnabled;

            var promptResult = await PromptBudgetTransactionInputAsync(budget, type);
            if (promptResult.Cancelled || !promptResult.Amount.HasValue)
                return;

            await RecordBudgetTransactionAsync(budget, type, promptResult.Amount.Value, promptResult);
        }

        public async Task SaveBudgetAsync(BudgetCardModel budget)
        {
            budget.NotifyTimeChanged(_getNow());

            if (budget.CardID > 0)
                await _budgets.SaveBudgetCardModelDataAsync(budget, budget.CardID);
            else
                await _cardWriter.SaveCardModelAsync(budget);
        }

        public async Task RecordBudgetTransactionAsync(BudgetCardModel budget, BudgetTransactionType type, double amount)
        {
            if (budget == null || amount <= 0)
                return;

            var metadata = await PromptUdmdForCardAsync(budget.CardID);
            if (metadata.Cancelled)
                return;

            await RecordBudgetTransactionAsync(budget, type, amount, metadata);
        }

        private async Task RecordBudgetTransactionAsync(
            BudgetCardModel budget,
            BudgetTransactionType type,
            double amount,
            UdmdPromptResult metadata)
        {
            if (budget == null || amount <= 0 || metadata.Cancelled)
                return;

            var transaction = new BudgetTransaction
            {
                Timestamp = _clock.UtcNow,
                Type = type,
                CurrencyAmount = amount,
                GlobalValueAmount = type == BudgetTransactionType.CashIn
                    ? amount * budget.ExchangeRate
                    : 0
            };

            budget.Transactions.Add(transaction);
            await SaveBudgetAsync(budget);
            await SaveBudgetTransactionMetadataIfNeededAsync(budget.CardID, transaction.Id, metadata);
        }

        private async Task<UdmdPromptResult> PromptBudgetTransactionInputAsync(
            BudgetCardModel budget,
            BudgetTransactionType type)
        {
            var isCashIn = type == BudgetTransactionType.CashIn;
            var title = isCashIn ? "Cash In" : "Spend";
            var message = $"How many {budget.Currency} do you want to {(isCashIn ? "cash in" : "spend")}?";
            var placeholder = isCashIn ? "e.g. 100" : "e.g. 250";
            var page = _pageService.CurrentPage;

            if (page != null)
            {
                return await UdmdPromptPage.PromptForBudgetTransactionAsync(
                    page,
                    _udmd,
                    budget.CardID,
                    _clock,
                    _navigation,
                    _dialogs,
                    title,
                    message,
                    placeholder);
            }

            var input = await _dialogs.DisplayPromptAsync(
                title,
                message,
                accept: "OK",
                cancel: "Cancel",
                placeholder: placeholder,
                keyboard: Keyboard.Numeric);

            if (TryParsePositiveAmount(input, out var amount))
                return new UdmdPromptResult { Amount = amount };

            if (!string.IsNullOrWhiteSpace(input))
                await _dialogs.DisplayAlertAsync("Invalid number", "Please enter a valid amount.", "OK");

            return UdmdPromptResult.CancelledResult;
        }

        private async Task AddTrackerValueAsync(TrackerCardModel card, double value, DateTime? timestampUtc)
        {
            var metadata = await PromptUdmdForCardAsync(card.CardID);
            if (metadata.Cancelled)
                return;

            var trackerValue = new TrackerValueModel
            {
                Timestamp = timestampUtc ?? _clock.UtcNow,
                Value = value
            };

            card.Values.Add(trackerValue);
            await SaveTrackerCardAsync(card);
            await SaveTrackerMetadataIfNeededAsync(card.CardID, trackerValue.Id, metadata);
        }

        private DateTime GetTrackerRegistrationLowerBoundLocal(TrackerCardModel card, DateTime nowLocal)
        {
            var lastValueLocal = card.Values.Count == 0
                ? (DateTime?)null
                : card.Values
                    .Select(value => TimeDisplayFormatter.ToLocalInstant(value.Timestamp, _timeZoneService))
                    .Max();

            var minLocal = lastValueLocal ?? GetTrackerStartLocal(card, nowLocal);

            return minLocal > nowLocal
                ? nowLocal
                : minLocal;
        }

        private DateTime GetTrackerStartLocal(TrackerCardModel card, DateTime nowLocal)
        {
            var start = card is EventTrackerCardModel && card.RangeStart != default
                ? card.RangeStart
                : card.CreatedDate != default
                    ? card.CreatedDate
                    : card.RangeStart != default
                        ? card.RangeStart
                        : nowLocal;

            return TimeDisplayFormatter.ToLocalInstant(start, _timeZoneService);
        }

        private async Task<UdmdPromptResult> PromptUdmdForCardAsync(long cardId)
        {
            var page = _pageService.CurrentPage;
            if (page == null || cardId <= 0)
                return UdmdPromptResult.Empty;

            return await UdmdPromptPage.PromptForCardAsync(page, _udmd, cardId, _clock, _navigation, _dialogs);
        }

        private async Task SaveTrackerCardAsync(TrackerCardModel tracker)
        {
            if (tracker is ValueTrackerCardModel valueTracker)
            {
                if (valueTracker.CardID > 0)
                    await _trackers.SaveValueTrackerCardModelDataAsync(valueTracker, valueTracker.CardID);
                else
                    await _cardWriter.SaveCardModelAsync(valueTracker);

                return;
            }

            if (tracker is EventTrackerCardModel eventTracker)
            {
                if (eventTracker.CardID > 0)
                    await _trackers.SaveEventTrackerCardModelDataAsync(eventTracker, eventTracker.CardID);
                else
                    await _cardWriter.SaveCardModelAsync(eventTracker);
            }
        }

        private async Task SaveTrackerMetadataIfNeededAsync(
            long cardId,
            long trackerValueId,
            UdmdPromptResult metadata)
        {
            if (metadata.Values.Count == 0)
                return;

            if (trackerValueId <= 0)
            {
                metadata.CleanupCreatedImages();
                return;
            }

            try
            {
                await _udmd.SaveTrackerValueMetadataAsync(cardId, trackerValueId, metadata.Values);
            }
            catch (Exception ex)
            {
                metadata.CleanupCreatedImages();
                await ShowMetadataSaveErrorAsync(ex);
            }
        }

        private async Task SaveBudgetTransactionMetadataIfNeededAsync(
            long cardId,
            long transactionId,
            UdmdPromptResult metadata)
        {
            if (metadata.Values.Count == 0)
                return;

            if (transactionId <= 0)
            {
                metadata.CleanupCreatedImages();
                return;
            }

            try
            {
                await _udmd.SaveBudgetTransactionMetadataAsync(cardId, transactionId, metadata.Values);
            }
            catch (Exception ex)
            {
                metadata.CleanupCreatedImages();
                await ShowMetadataSaveErrorAsync(ex);
            }
        }

        private Task ShowMetadataSaveErrorAsync(Exception ex)
        {
            return _dialogs.DisplayAlertAsync("Metadata not saved", ex.Message, "OK");
        }

        private static bool TryParsePositiveAmount(string? input, out double amount)
        {
            amount = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out amount) &&
                !double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out amount))
            {
                return false;
            }

            return amount > 0;
        }
    }
}
