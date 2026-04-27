namespace Points.Models
{
    public static class NotificationLogStatuses
    {
        public const string Created = "Created";
        public const string Scheduled = "Scheduled";
        public const string Sent = "Sent";
        public const string Missed = "Missed";
    }

    public sealed class NotificationLogModel
    {
        public long NotificationLogId { get; set; }
        public long ScheduleId { get; set; }
        public long CardId { get; set; }
        public string CardTitle { get; set; } = "";
        public string Note { get; set; } = "";
        public string Status { get; set; } = NotificationLogStatuses.Created;
        public DateTime CreatedAt { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime ScheduleFor { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Error { get; set; }
    }
}
