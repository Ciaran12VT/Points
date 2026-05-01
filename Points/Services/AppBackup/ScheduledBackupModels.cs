using Points.Models;

namespace Points.Services.Backup
{
    public enum ScheduledBackupDestinationType
    {
        DeviceStorage,
        GoogleDrive
    }

    public enum ScheduledBackupRunStatus
    {
        Success,
        Failed,
        Skipped,
        RequiresUserAction
    }

    public sealed class ScheduledBackupConfig
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public bool IsEnabled { get; set; }
        public ScheduledBackupSchedule Schedule { get; set; } = ScheduledBackupSchedule.Default();
        public List<string> ResourceKeys { get; set; } = new() { "database" };
        public ScheduledBackupDestinationConfig Destination { get; set; } = ScheduledBackupDestinationConfig.DeviceStorage();
        public int RetentionCount { get; set; } = 7;
        public DateTime? LastRunStartedAtUtc { get; set; }
        public DateTime? LastRunCompletedAtUtc { get; set; }
        public DateTime? NextRunAtLocal { get; set; }
        public string? LastErrorCode { get; set; }
        public string? LastErrorMessage { get; set; }
        public bool RequiresUserAction { get; set; }

        public static ScheduledBackupConfig DisabledDefault()
        {
            return new ScheduledBackupConfig();
        }
    }

    public sealed class ScheduledBackupSchedule : IScheduleModel
    {
        public FrequencyType FrequencyType { get; set; } = FrequencyType.EveryDays;
        public int FrequencyValue { get; set; } = 1;
        public DateTime FromDateTime { get; set; } = new(2000, 1, 1, 2, 0, 0, DateTimeKind.Unspecified);
        public DateTime? ToDateTime { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string? Note { get; set; } = "";

        public static ScheduledBackupSchedule Default()
        {
            return new ScheduledBackupSchedule();
        }
    }

    public sealed class ScheduledBackupDestinationConfig
    {
        public ScheduledBackupDestinationType Type { get; set; } = ScheduledBackupDestinationType.DeviceStorage;
        public string DisplayName { get; set; } = "App exports folder";
        public string? DeviceFolderPath { get; set; }
        public string? DeviceFolderUri { get; set; }
        public string? GoogleDriveAccountEmail { get; set; }
        public string? GoogleDriveFolderId { get; set; }
        public string? GoogleDriveFolderName { get; set; }
        public string? GoogleDriveCredentialKey { get; set; }

        public static ScheduledBackupDestinationConfig DeviceStorage()
        {
            return new ScheduledBackupDestinationConfig();
        }
    }

    public sealed class ScheduledBackupLogEntry
    {
        public Guid RunId { get; set; } = Guid.NewGuid();
        public DateTime StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public ScheduledBackupRunStatus Status { get; set; }
        public ScheduledBackupDestinationType DestinationType { get; set; }
        public string DestinationDisplayName { get; set; } = "";
        public string FileName { get; set; } = "";
        public string? FilePath { get; set; }
        public long? Bytes { get; set; }
        public List<string> ResourceKeys { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
