using Points.Evaluators;
using Points.Models;
using Points.Services;
using Points.Services.Locks;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Views.Udmd;
using Points.Views.Home;

namespace Points.ViewModels.Home
{
    internal sealed class HomeActivityInteractionCoordinator
    {
        private readonly IActivityService _activity;
        private readonly IUdmdService _udmd;
        private readonly IActiveCardNotificationService _activeCardNotificationService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly IPopupService _popups;
        private readonly IAppPageService _pageService;
        private readonly IClock _clock;
        private readonly IReadOnlyList<HomePageModel> _pages;
        private readonly Func<List<IActiveCardModel>> _getActiveCardModels;
        private readonly Action<IActiveCardModel?> _setActiveCard;
        private readonly Action _notifyActiveCardChanged;

        public HomeActivityInteractionCoordinator(
            IActivityService activity,
            IUdmdService udmd,
            IActiveCardNotificationService activeCardNotificationService,
            ITimeZoneService timeZoneService,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            IPopupService popups,
            IAppPageService pageService,
            IClock clock,
            IReadOnlyList<HomePageModel> pages,
            Func<List<IActiveCardModel>> getActiveCardModels,
            Action<IActiveCardModel?> setActiveCard,
            Action notifyActiveCardChanged)
        {
            _activity = activity;
            _udmd = udmd;
            _activeCardNotificationService = activeCardNotificationService;
            _timeZoneService = timeZoneService;
            _navigation = navigation;
            _dialogs = dialogs;
            _popups = popups;
            _pageService = pageService;
            _clock = clock;
            _pages = pages;
            _getActiveCardModels = getActiveCardModels;
            _setActiveCard = setActiveCard;
            _notifyActiveCardChanged = notifyActiveCardChanged;
        }

        public async Task RequestActivateAsync(IActiveCardModel card, DateTime? nowUtc = null)
        {
            if (card == null)
                return;

            try
            {
                var nowUtcNonNull = nowUtc.HasValue
                    ? StrictTimeSerializer.RequireUtcInstant(nowUtc.Value, nameof(nowUtc))
                    : _clock.UtcNow;
                var lockEvaluationNow = _timeZoneService.ToLocal(nowUtcNonNull);

                if (LockEvaluator.IsLockedNow(card, lockEvaluationNow, _getActiveCardModels(), out var availableAt))
                {
                    var remaining = LockEvaluator.FormatRemaining(lockEvaluationNow, availableAt);
                    await _dialogs.DisplayAlertAsync("Locked", $"This card is locked. Available in {remaining}.", "OK");
                    return;
                }

                var rateSnapshot = await GetValueRateSnapshotAsync(card);
                if (rateSnapshot == null)
                    return;

                var pendingMetadata = UdmdPromptResult.Empty;
                if (!card.IsActive)
                {
                    pendingMetadata = await PromptUdmdForCardAsync(card.CardID);
                    if (pendingMetadata.Cancelled)
                        return;
                }

                var result = await _activity.ToggleActivityAsync(
                    cardId: card.CardID,
                    utcNow: nowUtcNonNull,
                    valueRateName: rateSnapshot.Value.RateName,
                    valuePerMinute: rateSnapshot.Value.ValuePerMinute);

                if (result.Opened != null && pendingMetadata.Values.Count > 0)
                    await SaveOpenedActivityMetadataAsync(card.CardID, result.Opened.Id, pendingMetadata);

                IActiveCardModel? activeCard = null;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (result.Closed != null)
                    {
                        var closedCard = ResolveCard(result.Closed.CardID);
                        if (closedCard != null)
                        {
                            UpsertActivity(closedCard, result.Closed);
                            closedCard.IsActive = false;
                            RefreshComputedProperties(closedCard);
                        }
                    }

                    if (result.Opened != null)
                    {
                        var openedCard = ResolveCard(result.Opened.CardID) ?? card;

                        UpsertActivity(openedCard, result.Opened);
                        openedCard.IsActive = true;
                        RefreshComputedProperties(openedCard);

                        activeCard = openedCard;
                    }

                    _setActiveCard(activeCard);
                    _notifyActiveCardChanged();
                });

                _activeCardNotificationService.UpdateActiveCardNotification(activeCard);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        public void WireLongPress(ICardModel card)
        {
            if (card is not IActiveCardModel active)
                return;

            active.LongPressRequested -= OnCardLongPressRequested;
            active.LongPressRequested += OnCardLongPressRequested;
        }

        public void RestoreActiveCardFromOpenActivity(ActivityModel? openActivity)
        {
            IActiveCardModel? activeCard = null;

            if (openActivity != null)
            {
                activeCard = ResolveCard(openActivity.CardID);

                if (activeCard != null)
                {
                    var existing = activeCard.Activity.FirstOrDefault(activity => activity.Id == openActivity.Id);
                    if (existing == null)
                    {
                        activeCard.Activity.Add(openActivity);
                        (activeCard as ObservableObject)?.RaisePropertyChanged(nameof(IActiveCardModel.Activity));
                    }

                    activeCard.IsActive = true;
                }
            }

            _setActiveCard(activeCard);
            _activeCardNotificationService.UpdateActiveCardNotification(activeCard);
        }

        public async Task AddScFirstStepAsync(ScCardModel? model)
        {
            if (model == null || model.Steps.Count == 0)
                return;

            var now = _clock.LocalNow;

            if (LockEvaluator.IsLockedNow(model, now, _getActiveCardModels(), out var availableAt))
            {
                var remaining = LockEvaluator.FormatRemaining(now, availableAt);
                await _dialogs.DisplayAlertAsync("Locked", $"This card is locked. Available in {remaining}.", "OK");
                return;
            }

            var step = model.Steps[0];
            if (!step.IncrementCommand.CanExecute(null))
                return;

            step.IncrementCommand.Execute(null);
            await Task.Yield();
            await _activity.AddRepForStep(step.Id, _clock.UtcNow, step.StepValue);
        }

        private async void OnCardLongPressRequested(IActiveCardModel card)
        {
            if (card is null)
                return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var nowLocal = _clock.LocalNow;
                var activationLabel = card.IsActive ? "Ends at" : "Starts at";

                DateTime minUtc;

                if (card.IsActive)
                {
                    var startUtc = await _activity.GetCurrentOpenActivityStartUtcAsync(card.CardID);
                    if (startUtc == null)
                        return;

                    minUtc = startUtc.Value;
                }
                else
                {
                    var lastEndUtc = await _activity.GetLastClosedActivityEndUtcAsync();
                    minUtc = lastEndUtc ?? DateTime.MinValue;
                }

                var minLocal = minUtc == DateTime.MinValue
                    ? DateTime.MinValue
                    : _timeZoneService.ToLocal(minUtc);
                var maxLocal = nowLocal;
                var initialLocal = nowLocal.AddMinutes(-5);

                var chosenObj = await _popups.ShowPopupAsync(new EditActiveTimePopup(
                    activationTypeText: activationLabel,
                    selectedTime: initialLocal,
                    minTime: minLocal,
                    maxTime: maxLocal,
                    localNow: nowLocal));

                if (chosenObj is not DateTime chosenLocal)
                    return;

                var chosenUtc = _timeZoneService.ToUtcFromLocal(chosenLocal);
                await RequestActivateAsync(card, chosenUtc);
            });
        }

        private async Task<ValueRateSnapshot?> GetValueRateSnapshotAsync(IActiveCardModel card)
        {
            var rateName = "Base Rate";
            var valuePerMinute = card.ValuePerMinute;

            if (card is TatCardModel tat && tat.ValueRates.Count > 0 && !card.IsActive)
            {
                List<string> rateNames = ["Base Rate", .. tat.ValueRates.Select(x => x.RateName)];

                var choice = await _dialogs.DisplayActionSheetAsync(
                    "Choose Rate",
                    "Cancel",
                    null,
                    rateNames.ToArray()
                );

                if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
                    return null;

                tat.SelectedValueRateModel =
                    choice == "Base Rate"
                        ? null
                        : tat.ValueRates.FirstOrDefault(x => x.RateName == choice);

                rateName = tat.SelectedValueRateModel?.RateName ?? "Base Rate";
                valuePerMinute = tat.SelectedValueRateModel?.ValuePerMinute ?? tat.ValuePerMinute;
            }
            else if (card is TatCardModel tatNoRates)
            {
                rateName = tatNoRates.SelectedValueRateModel?.RateName ?? "Base Rate";
                valuePerMinute = tatNoRates.SelectedValueRateModel?.ValuePerMinute ?? tatNoRates.ValuePerMinute;
            }

            return new ValueRateSnapshot(rateName, valuePerMinute);
        }

        private async Task<UdmdPromptResult> PromptUdmdForCardAsync(long cardId)
        {
            var page = _pageService.CurrentPage;
            if (page == null || cardId <= 0)
                return UdmdPromptResult.Empty;

            return await UdmdPromptPage.PromptForCardAsync(page, _udmd, cardId, _clock, _navigation, _dialogs);
        }

        private async Task SaveOpenedActivityMetadataAsync(long cardId, int activityId, UdmdPromptResult metadata)
        {
            try
            {
                await _udmd.SaveActivityMetadataAsync(cardId, activityId, metadata.Values);
            }
            catch (Exception ex)
            {
                metadata.CleanupCreatedImages();
                await _dialogs.DisplayAlertAsync("Metadata not saved", ex.Message, "OK");
            }
        }

        private IActiveCardModel? ResolveCard(long cardId)
        {
            return _pages.SelectMany(page => page.AllCards)
                .OfType<IActiveCardModel>()
                .FirstOrDefault(card => card.CardID == cardId);
        }

        private static void UpsertActivity(IActiveCardModel target, ActivityModel activity)
        {
            var existing = target.Activity.FirstOrDefault(a => a.Id == activity.Id);

            if (existing == null)
            {
                target.Activity.Add(activity);
                return;
            }

            existing.CardID = activity.CardID;
            existing.StartDate = activity.StartDate;
            existing.EndDate = activity.EndDate;
            existing.RateName = activity.RateName;
            existing.ValuePerMinute = activity.ValuePerMinute;
        }

        private static void RefreshComputedProperties(IActiveCardModel model)
        {
            if (model is TatCardModel tatModel)
            {
                tatModel.RaisePropertyChanged(nameof(TatCardModel.ShowRateNameOnCard));
                tatModel.RaisePropertyChanged(nameof(TatCardModel.SelectedRateName));
                tatModel.RaisePropertyChanged(nameof(TatCardModel.Activity));
            }
            else
            {
                (model as ObservableObject)?.RaisePropertyChanged(nameof(IActiveCardModel.Activity));
            }

            (model as ObservableObject)?.RaisePropertyChanged(nameof(IActiveCardModel.IsActive));
        }

        private readonly record struct ValueRateSnapshot(string RateName, double ValuePerMinute);
    }
}
