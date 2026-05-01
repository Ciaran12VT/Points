using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Evaluators
{
    public class TimeValueAchievementEvaluator
    {
        public List<TimeValueAchievementEvaluation> Evaluations { get; set; } = new();

        public List<AchievementCardModel> CheckForEarnedAchievements(double additionalTime, double additionalValue, DateTime? evaluatedAt = null)
        {
            List<AchievementCardModel> earnedAchievements = new List<AchievementCardModel>();
            var now = evaluatedAt ?? ActivityTimeMath.LocalNow;

            if(Evaluations is null || Evaluations.Count == 0) return earnedAchievements;

            foreach (var eval in Evaluations)
            {
                double prog = 0;

                double valueToIncrement = 0;
                if (eval.AchievementCard.TargetType == AchievementTargetType.ActiveTime) valueToIncrement = additionalTime;
                else if(eval.AchievementCard.TargetType == AchievementTargetType.Value) valueToIncrement = additionalValue;

                prog = eval.IncrementAndGetValue(valueToIncrement);

                if(prog > 0)
                {
                    if (eval.AchievementCard.TargetType == AchievementTargetType.ActiveTime)
                    {
                        prog = prog / eval.AchievementCard.GetTargetSecondsSpent();
                    }
                    else if (eval.AchievementCard.TargetType == AchievementTargetType.Value)
                    {
                        prog = prog / eval.AchievementCard.TargetValue;
                    }

                    if (prog >= 1 && eval.MeetsConditionsForAchievement(now))
                    {
                        earnedAchievements.Add(eval.AchievementCard);
                        eval.CurrentValue = 0;
                        eval.AchievementCard.CompletedAt = now;
                    }
                }
            }

            return earnedAchievements;
        }
    }

    public class TimeValueAchievementEvaluation
    {
        public AchievementCardModel AchievementCard { get; set; } = null!;

        public double CurrentValue { get; set; }

        public double IncrementAndGetValue(double incrementBy)
        {
            CurrentValue += incrementBy;

            return CurrentValue;
        }

        public bool MeetsConditionsForAchievement(DateTime evaluatedAt)
        {
            if(AchievementCard.CompletionType == AchievementCompletionType.Range && AchievementCard.IsLockedThisRange)
            {
                return false;
            }

            if (AchievementCard.CompletionType == AchievementCompletionType.Deadline)
            {
                if(AchievementCard.Deadline > evaluatedAt)
                {
                    return false;
                }

                if(AchievementCard.CompletedAt != DateTime.MinValue)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
