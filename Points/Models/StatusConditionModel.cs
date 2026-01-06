namespace Points.Models
{
    public sealed class StatusConditionModel
    {
        public int StatusConditionID { get; set; }
        public string StatusConditionName { get; set; } = "";
        public double StatusConditionMultiplierValue { get; set; } = 1.0;
    }
}
