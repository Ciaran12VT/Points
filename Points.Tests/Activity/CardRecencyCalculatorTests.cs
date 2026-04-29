using Points.Services.Activity;
using Xunit;

namespace Points.Tests.Activity;

public sealed class CardRecencyCalculatorTests
{
    [Fact]
    public void Latest_ReturnsCandidateWhenItIsNewerThanBaseline()
    {
        var baseline = new DateTime(2026, 4, 29, 9, 0, 0, DateTimeKind.Utc);
        var rep = new DateTime(2026, 4, 29, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(rep, CardRecencyCalculator.Latest(baseline, new[] { rep }));
    }

    [Fact]
    public void Latest_KeepsBaselineWhenCandidatesAreOlder()
    {
        var baseline = new DateTime(2026, 4, 29, 11, 0, 0, DateTimeKind.Utc);
        var rep = new DateTime(2026, 4, 29, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(baseline, CardRecencyCalculator.Latest(baseline, new[] { rep }));
    }

    [Fact]
    public void Latest_HandlesEmptyCandidates()
    {
        var baseline = new DateTime(2026, 4, 29, 11, 0, 0, DateTimeKind.Utc);

        Assert.Equal(baseline, CardRecencyCalculator.Latest(baseline, Array.Empty<DateTime>()));
    }
}
