namespace Points.Models
{
    public enum PlannerTaskCardKind
    {
        TatCard,
        ScCard,
        Mission
    }

    public enum PlannerEventKind
    {
        ScStepRep,
        MissionComplete,
        MissionFail
    }

    public enum PlannerMatchStatus
    {
        Planned,
        FullMatch,
        PartialMatch,
        Missing,
        UnplannedActual
    }

    public sealed class PlannerModel
    {
        public long PlannerId { get; set; }
        public DateTime PlannerDate { get; set; } = LocalToday();
        public List<PlannerTaskModel> Tasks { get; set; } = new();
        public List<PlannerEventModel> Events { get; set; } = new();

        private static DateTime LocalToday()
        {
            return DateTime.SpecifyKind(ActivityTimeMath.LocalNow.Date, DateTimeKind.Unspecified);
        }
    }

    public sealed class PlannerTaskModel
    {
        public long PlannerTaskId { get; set; }
        public long PlannerId { get; set; }
        public long CardId { get; set; }
        public PlannerTaskCardKind CardKind { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
    }

    public sealed class PlannerEventModel
    {
        public long PlannerEventId { get; set; }
        public long PlannerId { get; set; }
        public PlannerEventKind EventKind { get; set; }
        public long CardId { get; set; }
        public int? ScCardStepId { get; set; }
        public DateTime PlannedTime { get; set; }
        public int PlannedCount { get; set; } = 1;
    }

    public sealed class PlannerDayData
    {
        public PlannerModel? Planner { get; init; }
        public List<IActiveCardModel> TaskCards { get; init; } = new();
        public List<ScCardModel> ScCards { get; init; } = new();
        public List<MissionCardModel> MissionCards { get; init; } = new();
    }
}
