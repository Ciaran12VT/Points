using Points.ViewModels.Home;
using Xunit;

namespace Points.Tests.Home;

public sealed class HomeLoadedRangeTrackerTests
{
    [Fact]
    public void RangeNeedsRefresh_UntilThatExactRangeLoadsSuccessfully()
    {
        var subject = new HomeLoadedRangeTracker();
        var oldStart = new DateTime(2026, 8, 21);
        var oldEnd = new DateTime(2026, 8, 22).AddTicks(-1);
        var newStart = new DateTime(2026, 8, 22);
        var newEnd = new DateTime(2026, 8, 23).AddTicks(-1);

        subject.MarkLoaded(oldStart, oldEnd);

        Assert.True(subject.IsLoaded(oldStart, oldEnd));
        Assert.False(subject.IsLoaded(newStart, newEnd));

        // A failed refresh never calls MarkLoaded, so the next reconciliation retries.
        Assert.False(subject.IsLoaded(newStart, newEnd));

        subject.MarkLoaded(newStart, newEnd);

        Assert.True(subject.IsLoaded(newStart, newEnd));
        Assert.False(subject.IsLoaded(oldStart, oldEnd));
    }
}
