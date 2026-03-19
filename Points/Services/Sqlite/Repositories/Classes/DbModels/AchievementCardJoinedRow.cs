namespace Points.Services.Sqlite
{
    public sealed partial class CardReadRepository
    {
        public sealed class AchievementCardJoinedRow
        {
            public int AchievementCardID { get; set; }
            public int CardID { get; set; }

            public string? Title { get; set; }
            public string? Tags { get; set; }

            public string? Status { get; set; }
            public string? Description { get; set; }
            public string? GoalType { get; set; }
            public string? DifficultyLevel { get; set; }

            public string CreatedDate { get; set; } = string.Empty;
            public string? LastEarnedAt { get; set; }

            public int? TargetActiveTimeInSeconds { get; set; }
            public double? TargetValue { get; set; }
            public int? ScCardStepID { get; set; }

            public string? CompletionType { get; set; }
            public string? RangeUnit { get; set; }
            public int? RangeAmount { get; set; }

            public string? DeadlineStart { get; set; }
            public string? Deadline { get; set; }

            public string? FinalizedAt { get; set; }
            public double? FrozenCurrentValue { get; set; }

            public string? TrophyURLs { get; set; }
            public int IsPinned { get; set; }
        }
    }
}