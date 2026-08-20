using Points.Models;
using Points.Services;
using Xunit;

namespace Points.Tests.Notifications;

public sealed class ActiveCardNotificationRequestTests
{
    [Fact]
    public void None_HasNoActivityPayload()
    {
        var request = ActiveCardNotificationRequest.None();

        Assert.Equal(ActiveCardNotificationMode.None, request.Mode);
        Assert.Null(request.ActiveCard);
        Assert.Null(request.DeadAirStartedAtUtc);
        Assert.False(request.AlertNoiseRequested);
    }

    [Fact]
    public void ForActiveCard_PreservesCardAndHasNoDeadAirStart()
    {
        var card = new TatCardModel { CardID = 42, Title = "Focus" };

        var request = ActiveCardNotificationRequest.ForActiveCard(card);

        Assert.Equal(ActiveCardNotificationMode.ActiveCard, request.Mode);
        Assert.Same(card, request.ActiveCard);
        Assert.Null(request.DeadAirStartedAtUtc);
        Assert.False(request.AlertNoiseRequested);
    }

    [Fact]
    public void ForDeadAir_RequiresAndPreservesUtcStart()
    {
        var startedAtUtc = new DateTime(2026, 8, 19, 9, 15, 0, DateTimeKind.Utc);

        var request = ActiveCardNotificationRequest.ForDeadAir(
            startedAtUtc,
            alertNoiseRequested: true);

        Assert.Equal(ActiveCardNotificationMode.DeadAir, request.Mode);
        Assert.Null(request.ActiveCard);
        Assert.Equal(startedAtUtc, request.DeadAirStartedAtUtc);
        Assert.True(request.AlertNoiseRequested);
        Assert.Throws<ArgumentException>(() =>
            ActiveCardNotificationRequest.ForDeadAir(DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void ForDeadAir_DefaultsAlertNoiseToNotRequested()
    {
        var request = ActiveCardNotificationRequest.ForDeadAir(
            new DateTime(2026, 8, 19, 9, 15, 0, DateTimeKind.Utc));

        Assert.False(request.AlertNoiseRequested);
    }
}
