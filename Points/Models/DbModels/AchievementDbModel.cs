namespace Points.Models.DbModels
{
    public class AchievementDbModel
    {
        public int AchievementID { get; set; }
        public int CardID { get; set; }

        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public string SubType { get; set; } = "";

        public DateTime CreatedDate { get; set; }
        public DateTime AvailableFromDate { get; set; }
        public DateTime DueDate { get; set; }

        public DateTime? CompletedDate { get; set; }
        public DateTime? LastEarnedAt { get; set; }

        public int? ScCardStepID { get; set; }

        public string ProgressType { get; set; } = "";
        public int? RangeAmount { get; set; }
        public DateTime? Deadline { get; set; }

        public string TrophyURLs { get; set; } = "";
    }

}
