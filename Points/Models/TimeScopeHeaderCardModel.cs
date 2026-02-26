namespace Points.Models
{
    public class TimeScopeHeaderCardModel : ICardModel
    {
        public int Id { get; set; }
        public long CardID { get; set; }
        public string Title { get; set; } = "";

        public string Tags { get; set; } = "";

        public double GetValue(DateTime start, DateTime end)
        {
            return 0;
        }
    }
}
