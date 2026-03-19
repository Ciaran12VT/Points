using SQLite;

namespace Points.Services.Sqlite
{
    public sealed partial class AchievementRepository
    {
        [Table("AchievementTrophy")]
        private sealed class AchievementTrophyRow
        {
            [PrimaryKey, AutoIncrement]
            public long TrophyID { get; set; }

            [Indexed]
            public long AchievementCardID { get; set; }

            public string Title { get; set; } = string.Empty;
            public string EarnedOn { get; set; } = string.Empty;
            public string ImageSource { get; set; } = string.Empty;
        }
    }
}