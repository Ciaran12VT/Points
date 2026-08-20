#if ANDROID
using Android.Content;
using Points.Services;

namespace Points.Platforms.Android;

/// <summary>
/// Persists handled Dead Air milestones with the UTC start instant they belong
/// to. Keeping the start alongside the bits prevents a previous interval from
/// suppressing alerts for a later interval.
/// </summary>
internal sealed class DeadAirAlertStateStore
{
    private const string StartTicksKey = "dead_air_alert_start_utc_ticks";
    private const string HandledMilestonesKey = "dead_air_alert_handled_milestones";
    private const string WasEligibleKey = "dead_air_alert_was_eligible";

    private readonly ISharedPreferences _preferences;

    public DeadAirAlertStateStore(Context context, string preferencesName)
    {
        _preferences = context.GetSharedPreferences(
            preferencesName,
            FileCreationMode.Private);
    }

    public bool TryRead(
        DateTime startedAtUtc,
        out DeadAirAlertMilestones handledMilestones,
        out bool wasEligible)
    {
        handledMilestones = default;
        wasEligible = false;

        if (startedAtUtc.Kind != DateTimeKind.Utc)
            return false;

        var storedStartTicks = _preferences.GetLong(StartTicksKey, long.MinValue);
        if (storedStartTicks != startedAtUtc.Ticks)
            return false;

        var bits = Math.Max(0, _preferences.GetInt(HandledMilestonesKey, 0));
        handledMilestones = new DeadAirAlertMilestones(
            ShortCueHandled: (bits & 1) != 0,
            LongCueHandled: (bits & 2) != 0);
        wasEligible = _preferences.GetBoolean(WasEligibleKey, false);
        return true;
    }

    public bool Write(
        DateTime startedAtUtc,
        DeadAirAlertMilestones handledMilestones,
        bool wasEligible)
    {
        if (startedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Dead Air alert state must be keyed by a UTC instant.", nameof(startedAtUtc));

        var bits = (handledMilestones.ShortCueHandled ? 1 : 0)
                   | (handledMilestones.LongCueHandled ? 2 : 0);

        return _preferences.Edit()!
            .PutLong(StartTicksKey, startedAtUtc.Ticks)
            .PutInt(HandledMilestonesKey, bits)
            .PutBoolean(WasEligibleKey, wasEligible)
            .Commit();
    }
}
#endif
