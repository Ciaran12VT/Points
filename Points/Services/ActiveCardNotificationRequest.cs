using Points.Models;
using Points.Services.Time;

namespace Points.Services;

public enum ActiveCardNotificationMode
{
    None,
    ActiveCard,
    DeadAir
}

public sealed record ActiveCardNotificationRequest
{
    private ActiveCardNotificationRequest(
        ActiveCardNotificationMode mode,
        IActiveCardModel? activeCard,
        DateTime? deadAirStartedAtUtc,
        bool alertNoiseRequested)
    {
        Mode = mode;
        ActiveCard = activeCard;
        DeadAirStartedAtUtc = deadAirStartedAtUtc;
        AlertNoiseRequested = alertNoiseRequested;
    }

    public ActiveCardNotificationMode Mode { get; }

    public IActiveCardModel? ActiveCard { get; }

    public DateTime? DeadAirStartedAtUtc { get; }

    public bool AlertNoiseRequested { get; }

    public static ActiveCardNotificationRequest None()
    {
        return new ActiveCardNotificationRequest(
            ActiveCardNotificationMode.None,
            activeCard: null,
            deadAirStartedAtUtc: null,
            alertNoiseRequested: false);
    }

    public static ActiveCardNotificationRequest ForActiveCard(IActiveCardModel activeCard)
    {
        ArgumentNullException.ThrowIfNull(activeCard);

        return new ActiveCardNotificationRequest(
            ActiveCardNotificationMode.ActiveCard,
            activeCard,
            deadAirStartedAtUtc: null,
            alertNoiseRequested: false);
    }

    public static ActiveCardNotificationRequest ForDeadAir(
        DateTime deadAirStartedAtUtc,
        bool alertNoiseRequested = false)
    {
        deadAirStartedAtUtc = StrictTimeSerializer.RequireUtcInstant(
            deadAirStartedAtUtc,
            nameof(deadAirStartedAtUtc));

        return new ActiveCardNotificationRequest(
            ActiveCardNotificationMode.DeadAir,
            activeCard: null,
            deadAirStartedAtUtc,
            alertNoiseRequested);
    }
}
