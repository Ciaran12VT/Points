namespace Points.Models
{
    public class ActivityModel
    {
        public int Id { get; set; }
        public long CardID { get; set; }

        public ActivityModel(DateTime start, DateTime? end, string rate, double value)
        {
            StartDate = start;
            EndDate = end;
            RateName = rate;
            ValuePerMinute = value;
        }

        public ActivityModel() { }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string RateName { get; set; } = "";
        public double ValuePerMinute { get; set; }
    }
}
