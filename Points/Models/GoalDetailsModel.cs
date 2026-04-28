using Points.ViewModels;

namespace Points.Models
{
    public class PlannerGoalDetailsModel
    {
        public int Id { get; set; }

        public long CardId { get; set; }

        public TimeScope TimeScope { get; set; }

        public double GoalHrs { get; set; }

        public TimeOnly? DeFactoStart { get; set; }
        public TimeOnly? DeFactoEnd { get; set; }

        public bool Enabled { get; set; } = false;
    }
}
