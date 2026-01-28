using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services
{
    public static class AchievementService
    {
        public static List<IActiveCardModel>? ActiveCardModels = null;

        public static double CalculateAchievementProgress(AchievementCardModel achievementCardModel)
        {
            double retval = 0;

            if(achievementCardModel.GoalType == AchievementGoalType.ActiveTime)
            {
                if(ActiveCardModels != null)
                {
                    var targetedCards = ActiveCardModels.Where(x => x.Tags.Contains(achievementCardModel.Tags));

                    if(targetedCards != null)
                    {
                        double totalSecondsSpent = 0;

                        DateTime startOfRange = GetRangeStart(achievementCardModel.RangeAmount, achievementCardModel.RangeUnit);

                        foreach (var card in targetedCards)
                        {
                            totalSecondsSpent += card.GetActiveTime(startOfRange, DateTime.Now).TotalSeconds;
                        }

                        double secs = achievementCardModel.GetTargeSecondsSpent();

                        retval = (totalSecondsSpent / secs) >= 1 ? 1 : (totalSecondsSpent / secs);
                    }
                }
            }

            return retval;
        }

        private static DateTime GetRangeStart(int rangeAmount, AchievementRangeUnit rangeUnit)
        {
            switch (rangeUnit)
            {
                case AchievementRangeUnit.Minutes:
                    return DateTime.Now.AddMinutes(rangeAmount * -1);
                case AchievementRangeUnit.Hours:
                    return DateTime.Now.AddHours(rangeAmount * -1);
                case AchievementRangeUnit.Days:
                    return DateTime.Now.AddDays(rangeAmount * -1);
                case AchievementRangeUnit.Weeks:
                    return DateTime.Now.AddDays((rangeAmount * 7) * -1);
                case AchievementRangeUnit.Months:
                    return DateTime.Now.AddMonths(rangeAmount * -1);
                default:
                    return DateTime.MinValue;
            }
        }
    }
}
