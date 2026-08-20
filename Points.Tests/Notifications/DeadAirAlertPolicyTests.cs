using Points.Services;
using Xunit;

namespace Points.Tests.Notifications;

public sealed class DeadAirAlertPolicyTests
{
    [Fact]
    public void Evaluate_BeforeThirtySeconds_DoesNothing()
    {
        var state = ArmAtStart();

        var decision = Evaluate(state, Seconds(29.999));

        Assert.Equal(DeadAirAlertAudioCommand.None, decision.AudioCommand);
        Assert.Equal(default, decision.State.Milestones);
        Assert.False(decision.MilestonesChanged);
    }

    [Fact]
    public void Evaluate_AtThirtySeconds_PlaysShortCueOnce()
    {
        var state = ArmAtStart();

        var first = Evaluate(state, Seconds(30));
        var repeated = Evaluate(first.State, Seconds(30));

        Assert.Equal(DeadAirAlertAudioCommand.PlayShortCue, first.AudioCommand);
        Assert.True(first.State.Milestones.ShortCueHandled);
        Assert.False(first.State.Milestones.LongCueHandled);
        Assert.True(first.MilestonesChanged);
        Assert.Equal(DeadAirAlertAudioCommand.None, repeated.AudioCommand);
        Assert.False(repeated.MilestonesChanged);
    }

    [Fact]
    public void Evaluate_AtFortyFiveSeconds_PlaysLongCueOnce()
    {
        var state = Evaluate(ArmAtStart(), Seconds(30)).State;

        var first = Evaluate(state, Seconds(45));
        var repeated = Evaluate(first.State, Seconds(45));

        Assert.Equal(DeadAirAlertAudioCommand.PlayLongCue, first.AudioCommand);
        Assert.Equal(
            new DeadAirAlertMilestones(ShortCueHandled: true, LongCueHandled: true),
            first.State.Milestones);
        Assert.True(first.MilestonesChanged);
        Assert.Equal(DeadAirAlertAudioCommand.None, repeated.AudioCommand);
    }

    [Fact]
    public void Evaluate_AtSixtySeconds_StartsLoopImmediately()
    {
        var state = Evaluate(ArmAtStart(), Seconds(45)).State;

        var first = Evaluate(state, Seconds(60));
        var repeated = Evaluate(first.State, Seconds(61));

        Assert.Equal(DeadAirAlertAudioCommand.StartLoop, first.AudioCommand);
        Assert.True(first.State.IsLoopRequested);
        Assert.Equal(DeadAirAlertAudioCommand.None, repeated.AudioCommand);
    }

    [Fact]
    public void Evaluate_DelayedPastBothOneShots_PlaysOnlyLongCue()
    {
        var state = ArmAtStart();

        var decision = Evaluate(state, Seconds(46));

        Assert.Equal(DeadAirAlertAudioCommand.PlayLongCue, decision.AudioCommand);
        Assert.Equal(
            new DeadAirAlertMilestones(ShortCueHandled: true, LongCueHandled: true),
            decision.State.Milestones);
    }

    [Fact]
    public void Evaluate_DelayedPastLoopThreshold_StartsOnlyLoop()
    {
        var state = ArmAtStart();

        var decision = Evaluate(state, Seconds(65));

        Assert.Equal(DeadAirAlertAudioCommand.StartLoop, decision.AudioCommand);
        Assert.True(decision.State.Milestones.ShortCueHandled);
        Assert.True(decision.State.Milestones.LongCueHandled);
    }

    [Theory]
    [InlineData(35)]
    [InlineData(50)]
    public void Evaluate_FirstEnableAfterOneShotThresholds_ConsumesWithoutBackfill(double elapsedSeconds)
    {
        var state = DeadAirAlertState.Initial();

        var decision = Evaluate(state, Seconds(elapsedSeconds));

        Assert.Equal(DeadAirAlertAudioCommand.None, decision.AudioCommand);
        Assert.True(decision.State.Milestones.ShortCueHandled);
        Assert.Equal(elapsedSeconds >= 45, decision.State.Milestones.LongCueHandled);
    }

    [Fact]
    public void Evaluate_FirstEnableAfterSixtySeconds_StartsLoop()
    {
        var decision = Evaluate(DeadAirAlertState.Initial(), Seconds(65));

        Assert.Equal(DeadAirAlertAudioCommand.StartLoop, decision.AudioCommand);
        Assert.True(decision.State.IsLoopRequested);
    }

    [Fact]
    public void Evaluate_ReEnableDoesNotBackfillMissedOneShot()
    {
        var state = ArmAtStart();
        state = DeadAirAlertPolicy.Evaluate(
            state,
            Seconds(20),
            alertNoiseRequested: false,
            notificationVisible: true).State;

        var decision = Evaluate(state, Seconds(35));

        Assert.Equal(DeadAirAlertAudioCommand.None, decision.AudioCommand);
        Assert.True(decision.State.Milestones.ShortCueHandled);
    }

    [Fact]
    public void Evaluate_ReEnableAfterSixtySeconds_RestartsLoop()
    {
        var looping = Evaluate(ArmAtStart(), Seconds(60));
        var stopped = DeadAirAlertPolicy.Evaluate(
            looping.State,
            Seconds(61),
            alertNoiseRequested: false,
            notificationVisible: true);

        var resumed = Evaluate(stopped.State, Seconds(62));

        Assert.Equal(DeadAirAlertAudioCommand.StopAudio, stopped.AudioCommand);
        Assert.False(stopped.State.IsLoopRequested);
        Assert.Equal(DeadAirAlertAudioCommand.StartLoop, resumed.AudioCommand);
        Assert.True(resumed.State.IsLoopRequested);
    }

    [Fact]
    public void Evaluate_WhileNotificationHidden_ConsumesOneShotsAndStopsAudio()
    {
        var state = Evaluate(ArmAtStart(), Seconds(30)).State;

        var hidden = DeadAirAlertPolicy.Evaluate(
            state,
            Seconds(45),
            alertNoiseRequested: true,
            notificationVisible: false);
        var restored = Evaluate(hidden.State, Seconds(50));

        Assert.Equal(DeadAirAlertAudioCommand.StopAudio, hidden.AudioCommand);
        Assert.True(hidden.State.Milestones.LongCueHandled);
        Assert.Equal(DeadAirAlertAudioCommand.None, restored.AudioCommand);
    }

    [Fact]
    public void Evaluate_ShortCueSuppressedByHiddenNotification_IsNotReplayed()
    {
        var hidden = DeadAirAlertPolicy.Evaluate(
            ArmAtStart(),
            Seconds(31),
            alertNoiseRequested: true,
            notificationVisible: false);
        var restored = Evaluate(hidden.State, Seconds(35));

        Assert.Equal(DeadAirAlertAudioCommand.StopAudio, hidden.AudioCommand);
        Assert.True(hidden.State.Milestones.ShortCueHandled);
        Assert.Equal(DeadAirAlertAudioCommand.None, restored.AudioCommand);
    }

    [Fact]
    public void Evaluate_NotificationRestoredAfterSixtySeconds_StartsLoop()
    {
        var state = DeadAirAlertPolicy.Evaluate(
            ArmAtStart(),
            Seconds(59),
            alertNoiseRequested: true,
            notificationVisible: false).State;

        var restored = Evaluate(state, Seconds(65));

        Assert.Equal(DeadAirAlertAudioCommand.StartLoop, restored.AudioCommand);
    }

    [Fact]
    public void Initial_WithRestoredMilestones_DoesNotReplayHandledCue()
    {
        var restored = DeadAirAlertState.Initial(
            new DeadAirAlertMilestones(ShortCueHandled: true, LongCueHandled: false));

        var activated = Evaluate(restored, Seconds(40));
        var atLongThreshold = Evaluate(activated.State, Seconds(45));

        Assert.Equal(DeadAirAlertAudioCommand.None, activated.AudioCommand);
        Assert.Equal(DeadAirAlertAudioCommand.PlayLongCue, atLongThreshold.AudioCommand);
    }

    [Fact]
    public void Initial_FirstEnablePastUnhandledThreshold_ConsumesIt()
    {
        var restored = DeadAirAlertState.Initial(
            new DeadAirAlertMilestones(ShortCueHandled: true, LongCueHandled: false));

        var decision = Evaluate(restored, Seconds(50));

        Assert.Equal(DeadAirAlertAudioCommand.None, decision.AudioCommand);
        Assert.True(decision.State.Milestones.LongCueHandled);
        Assert.True(decision.MilestonesChanged);
    }

    [Fact]
    public void Restore_ArmedMatchingSession_PlaysHighestUnhandledCueAfterDelay()
    {
        var restored = DeadAirAlertState.Restore(
            new DeadAirAlertMilestones(ShortCueHandled: true, LongCueHandled: false),
            wasEligible: true);

        var decision = Evaluate(restored, Seconds(46));

        Assert.Equal(DeadAirAlertAudioCommand.PlayLongCue, decision.AudioCommand);
        Assert.True(decision.State.Milestones.LongCueHandled);
    }

    [Fact]
    public void Restore_ArmedMatchingSession_PlaysUnhandledShortCueAfterDelay()
    {
        var restored = DeadAirAlertState.Restore(
            restoredMilestones: default,
            wasEligible: true);

        var decision = Evaluate(restored, Seconds(35));

        Assert.Equal(DeadAirAlertAudioCommand.PlayShortCue, decision.AudioCommand);
        Assert.True(decision.State.Milestones.ShortCueHandled);
        Assert.False(decision.State.Milestones.LongCueHandled);
    }

    [Fact]
    public void Restore_ArmedMatchingSession_EmitsOnlyHighestOfAllUnhandledCues()
    {
        var restored = DeadAirAlertState.Restore(
            restoredMilestones: default,
            wasEligible: true);

        var decision = Evaluate(restored, Seconds(46));

        Assert.Equal(DeadAirAlertAudioCommand.PlayLongCue, decision.AudioCommand);
        Assert.Equal(
            new DeadAirAlertMilestones(ShortCueHandled: true, LongCueHandled: true),
            decision.State.Milestones);
    }

    [Fact]
    public void Restore_UnarmedMatchingSession_DoesNotBackfillCue()
    {
        var restored = DeadAirAlertState.Restore(
            restoredMilestones: default,
            wasEligible: false);

        var decision = Evaluate(restored, Seconds(46));

        Assert.Equal(DeadAirAlertAudioCommand.None, decision.AudioCommand);
        Assert.Equal(
            new DeadAirAlertMilestones(ShortCueHandled: true, LongCueHandled: true),
            decision.State.Milestones);
    }

    [Fact]
    public void Initial_NormalizesImpossibleRestoredMilestones()
    {
        var state = DeadAirAlertState.Initial(
            new DeadAirAlertMilestones(ShortCueHandled: false, LongCueHandled: true));

        Assert.True(state.Milestones.ShortCueHandled);
        Assert.True(state.Milestones.LongCueHandled);
    }

    [Fact]
    public void Evaluate_RejectsNegativeElapsedTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Evaluate(DeadAirAlertState.Initial(), TimeSpan.FromMilliseconds(-1)));
    }

    private static DeadAirAlertState ArmAtStart()
    {
        return Evaluate(DeadAirAlertState.Initial(), TimeSpan.Zero).State;
    }

    private static DeadAirAlertDecision Evaluate(
        DeadAirAlertState state,
        TimeSpan elapsed)
    {
        return DeadAirAlertPolicy.Evaluate(
            state,
            elapsed,
            alertNoiseRequested: true,
            notificationVisible: true);
    }

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
}
