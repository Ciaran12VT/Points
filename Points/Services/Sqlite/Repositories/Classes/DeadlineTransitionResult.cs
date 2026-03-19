namespace Points.Services.Sqlite
{
    public sealed partial class AchievementRepository
    {
        private enum DeadlineTransitionResult
        {
            None = 0,
            Complete = 1,
            Fail = 2
        }
    }
}