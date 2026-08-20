namespace Points.Services;

public enum DeadAirAlertAudioCommand
{
    None,
    StopAudio,
    PlayShortCue,
    PlayLongCue,
    StartLoop
}

public readonly record struct DeadAirAlertMilestones(
    bool ShortCueHandled,
    bool LongCueHandled);

public readonly record struct DeadAirAlertState
{
    private DeadAirAlertState(
        bool isInitialized,
        bool wasEligible,
        DeadAirAlertMilestones milestones,
        bool isLoopRequested)
    {
        IsInitialized = isInitialized;
        WasEligible = wasEligible;
        Milestones = milestones;
        IsLoopRequested = isLoopRequested;
    }

    public bool IsInitialized { get; }

    public bool WasEligible { get; }

    public DeadAirAlertMilestones Milestones { get; }

    public bool IsLoopRequested { get; }

    public static DeadAirAlertState Initial(
        DeadAirAlertMilestones restoredMilestones = default)
    {
        return new DeadAirAlertState(
            isInitialized: false,
            wasEligible: false,
            Normalize(restoredMilestones),
            isLoopRequested: false);
    }

    /// <summary>
    /// Restores a matching Dead Air interval after process recreation. Pass the
    /// eligibility that was durably recorded for that interval so an armed
    /// session can still emit the highest unhandled cue after a delayed restart,
    /// while an unarmed session retains no-backfill behavior.
    /// </summary>
    public static DeadAirAlertState Restore(
        DeadAirAlertMilestones restoredMilestones,
        bool wasEligible)
    {
        return new DeadAirAlertState(
            isInitialized: true,
            wasEligible,
            Normalize(restoredMilestones),
            isLoopRequested: false);
    }

    internal DeadAirAlertState Next(
        bool isEligible,
        DeadAirAlertMilestones milestones,
        bool isLoopRequested)
    {
        return new DeadAirAlertState(
            isInitialized: true,
            wasEligible: isEligible,
            Normalize(milestones),
            isLoopRequested);
    }

    private static DeadAirAlertMilestones Normalize(DeadAirAlertMilestones milestones)
    {
        return milestones.LongCueHandled && !milestones.ShortCueHandled
            ? milestones with { ShortCueHandled = true }
            : milestones;
    }
}

public readonly record struct DeadAirAlertDecision(
    DeadAirAlertState State,
    DeadAirAlertAudioCommand AudioCommand,
    bool MilestonesChanged);

/// <summary>
/// Pure policy for one uninterrupted Dead Air interval. The Android foreground
/// service owns scheduling, persistence, and audio playback; this type decides
/// which milestone is due and prevents replay within the interval.
/// </summary>
public static class DeadAirAlertPolicy
{
    public static readonly TimeSpan ShortCueThreshold = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan LongCueThreshold = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan LoopThreshold = TimeSpan.FromSeconds(60);

    public static DeadAirAlertDecision Evaluate(
        DeadAirAlertState state,
        TimeSpan elapsed,
        bool alertNoiseRequested,
        bool notificationVisible)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Elapsed time cannot be negative.");

        var previousMilestones = state.Milestones;
        var shortCueHandled = previousMilestones.ShortCueHandled;
        var longCueHandled = previousMilestones.LongCueHandled;
        var isEligible = alertNoiseRequested && notificationVisible;
        var becameEligible = isEligible && (!state.IsInitialized || !state.WasEligible);
        var command = DeadAirAlertAudioCommand.None;
        var loopRequested = state.IsLoopRequested;

        if (elapsed >= LoopThreshold)
        {
            shortCueHandled = true;
            longCueHandled = true;

            if (isEligible)
            {
                if (!loopRequested)
                    command = DeadAirAlertAudioCommand.StartLoop;

                loopRequested = true;
            }
            else
            {
                if (loopRequested || state.WasEligible)
                    command = DeadAirAlertAudioCommand.StopAudio;

                loopRequested = false;
            }
        }
        else
        {
            if (loopRequested || (state.WasEligible && !isEligible))
                command = DeadAirAlertAudioCommand.StopAudio;

            loopRequested = false;

            if (!isEligible || becameEligible)
            {
                // Suppressed and newly enabled cues are consumed, not backfilled.
                if (elapsed >= ShortCueThreshold)
                    shortCueHandled = true;

                if (elapsed >= LongCueThreshold)
                    longCueHandled = true;
            }
            else if (elapsed >= LongCueThreshold && !longCueHandled)
            {
                // A delayed evaluation emits only the highest applicable cue.
                shortCueHandled = true;
                longCueHandled = true;
                command = DeadAirAlertAudioCommand.PlayLongCue;
            }
            else if (elapsed >= ShortCueThreshold && !shortCueHandled)
            {
                shortCueHandled = true;
                command = DeadAirAlertAudioCommand.PlayShortCue;
            }
        }

        var milestones = new DeadAirAlertMilestones(shortCueHandled, longCueHandled);
        var nextState = state.Next(isEligible, milestones, loopRequested);

        return new DeadAirAlertDecision(
            nextState,
            command,
            MilestonesChanged: milestones != previousMilestones);
    }
}
