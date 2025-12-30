namespace Points.Models.DbModels
{
    public class AchievementTrophyDbModel
    {
        public int TrophyID { get; set; }
        public int AchievementID { get; set; }

        public string Title { get; set; } = "";
        public DateTime EarnedOn { get; set; }
        public string ImageSource { get; set; } = "";
    }

}
