namespace Points.Global;

public static class AppPaths
{
    public static string BackupAutomationFolder
    {
        get
        {
            var path = Path.Combine(Path.GetTempPath(), "PointsBackupAutomationTests");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string BackupAutomationConfigPath => Path.Combine(BackupAutomationFolder, "backup_automation.json");
    public static string BackupAutomationLogPath => Path.Combine(BackupAutomationFolder, "backup_automation.log.jsonl");
    public static string GoogleDriveOAuthClientConfigPath => Path.Combine(BackupAutomationFolder, "google_drive_oauth_client.json");

    public static string ScheduledBackupExportsFolder
    {
        get
        {
            var path = Path.Combine(Path.GetTempPath(), "PointsBackupAutomationTests", "exports");
            Directory.CreateDirectory(path);
            return path;
        }
    }

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
