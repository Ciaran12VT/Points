namespace Points.Global;

public static class AppPaths
{
    public static string GetAchievementTrophiesPath(int achievementId)
    {
        var path = Path.Combine(Path.GetTempPath(), "PointsAchievementTests", $"AchievementID_{achievementId}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetImageMetadataPath(long cardId)
    {
        var path = Path.Combine(Path.GetTempPath(), "PointsCardTests", $"CardID_{cardId}");
        Directory.CreateDirectory(path);
        return path;
    }
}
