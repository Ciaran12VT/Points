namespace Points.Services.Sqlite
{
    public sealed partial class CardReadRepository
    {
        private sealed class TrophyRow
        {
            public int Id { get; set; }
            public int AchievementId { get; set; }
            public string? Title { get; set; }
            public string EarnedOn { get; set; } = string.Empty;
            public string? ImageSource { get; set; }
        }
    }
}