using Points.Global;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.Services;

public sealed class ActiveCardNotificationService : IActiveCardNotificationService
{
    private readonly IActiveCardNotificationPresenter _presenter;
    private readonly ISettingsService _settings;
    private readonly IActivityService _activity;
    private readonly IClock _clock;
    private readonly ITimeZoneService _timeZoneService;
    private readonly SemaphoreSlim _reconcileGate = new(1, 1);

    public ActiveCardNotificationService(
        IActiveCardNotificationPresenter presenter,
        ISettingsService settings,
        IActivityService activity,
        IClock clock,
        ITimeZoneService timeZoneService)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
    }

    public async Task ReconcileAsync(
        IActiveCardModel? activeCard,
        CancellationToken cancellationToken = default)
    {
        await _reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var request = activeCard is not null
                ? ActiveCardNotificationRequest.ForActiveCard(activeCard)
                : await BuildInactiveRequestAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            await _presenter.PresentAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _reconcileGate.Release();
        }
    }

    private async Task<ActiveCardNotificationRequest> BuildInactiveRequestAsync(
        CancellationToken cancellationToken)
    {
        var settings = await _settings.GetSettingsAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var deadAirNotificationEnabled = settings
            .FirstOrDefault(setting => setting.SettingKey == SettingKeys.DeadAirNotificationEnabled)
            ?.BoolValue ?? false;

        if (!deadAirNotificationEnabled)
            return ActiveCardNotificationRequest.None();

        var alertNoiseEnabled = settings
            .FirstOrDefault(setting => setting.SettingKey == SettingKeys.DeadAirAlertNoiseEnabled)
            ?.BoolValue ?? false;

        var nowUtc = StrictTimeSerializer.RequireUtcInstant(_clock.UtcNow, nameof(IClock.UtcNow));
        var localNow = StrictTimeSerializer.RequireWallClockDateTime(_clock.LocalNow, nameof(IClock.LocalNow));
        var fallbackStartUtc = _timeZoneService.ToUtcFromLocal(localNow.Date);

        var lastClosedEndUtc = await _activity.GetLastClosedActivityEndUtcAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var deadAirStartedAtUtc = fallbackStartUtc;

        if (lastClosedEndUtc.HasValue)
        {
            try
            {
                deadAirStartedAtUtc = StrictTimeSerializer.RequireUtcInstant(
                    lastClosedEndUtc.Value,
                    nameof(lastClosedEndUtc));
            }
            catch (ArgumentException)
            {
                deadAirStartedAtUtc = nowUtc;
            }
        }

        if (deadAirStartedAtUtc > nowUtc)
            deadAirStartedAtUtc = nowUtc;

        return ActiveCardNotificationRequest.ForDeadAir(
            deadAirStartedAtUtc,
            alertNoiseRequested: deadAirNotificationEnabled && alertNoiseEnabled);
    }
}
