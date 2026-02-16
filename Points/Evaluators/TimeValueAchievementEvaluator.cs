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
        public List<TimeValueAchievementEvaluation> Evaluations { get; set; }

        public List<AchievementCardModel> CheckForEarnedAchievements(double additionalTime, double additionalValue)
        {
            List<AchievementCardModel> earnedAchievements = new List<AchievementCardModel>();

            if(Evaluations is null || Evaluations.Count == 0) return earnedAchievements;

            foreach (var eval in Evaluations)
            {
                double prog = 0;

                double valueToIncrement = 0;
                if (eval.AchievemenCard.GoalType == AchievementGoalType.ActiveTime) valueToIncrement = additionalTime;
                else if(eval.AchievemenCard.GoalType == AchievementGoalType.Value) valueToIncrement = additionalValue;

                prog = eval.IncrementAndGetValue(valueToIncrement);

                if(prog > 0)
                {
                    if (eval.AchievemenCard.GoalType == AchievementGoalType.ActiveTime)
                    {
                        prog = prog / eval.AchievemenCard.GetTargetSecondsSpent();
                    }
                    else if (eval.AchievemenCard.GoalType == AchievementGoalType.Value)
                    {
                        prog = prog / eval.AchievemenCard.TargetValue;
                    }

                    if (prog >= 1 && !eval.AchievemenCard.IsLockedThisRange)
                    {
                        earnedAchievements.Add(eval.AchievemenCard);
                        eval.CurrentValue = 0;
                    }
                }
            }

            return earnedAchievements;
        }
    }

    public class TimeValueAchievementEvaluation
    {
        public AchievementCardModel AchievemenCard {  get; set; } 

        public double CurrentValue { get; set; }

        public double IncrementAndGetValue(double incrementBy)
        {
            CurrentValue += incrementBy;

            return CurrentValue;
        }
    }
}
