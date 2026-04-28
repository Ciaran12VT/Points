namespace Points.Tests.Time;

internal static class TestTimeZones
{
    public static TimeZoneInfo Dublin => FindTimeZone("GMT Standard Time", "Europe/Dublin");

    private static TimeZoneInfo FindTimeZone(params string[] ids)
    {
        foreach (var id in ids)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException($"Could not find any of these test time zones: {string.Join(", ", ids)}.");
    }
}
