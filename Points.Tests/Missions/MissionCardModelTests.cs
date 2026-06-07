using Points.Models;
using Xunit;

namespace Points.Tests.Missions;

public sealed class MissionCardModelTests
{
    [Fact]
    public void Restore_ClearsFailedCompletionStateAndReenablesCompletion()
    {
        var mission = new MissionCardModel();
        var failedAt = new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);

        mission.Fail(failedAt);

        mission.Restore();

        Assert.False(mission.IsFailed);
        Assert.False(mission.IsComplete);
        Assert.Null(mission.CompletedDate);
        Assert.Equal("In-Progress", mission.Status);
        Assert.True(mission.CompleteCommand.CanExecute(null));
    }
}
