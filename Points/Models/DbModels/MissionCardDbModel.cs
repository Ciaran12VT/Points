namespace Points.Models.DbModels
{
    public class MissionCardDbModel
    {
        public int MissionCardID { get; set; }
        public int CardID { get; set; }

        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public string SubType { get; set; } = "";

        public double Value { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime AvailableFromDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public string EstCompletionTimeText { get; set; } = "";

        public bool IsFailed { get; set; }
        public double ValuePerMinute { get; set; }
    }

}
