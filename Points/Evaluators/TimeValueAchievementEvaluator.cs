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
        public List<ITimeValueAchievementEvaluation> Evaluations;

        public List<AchievementCardModel> CheckForEarnedAchievements(double additionalTime, double additionalValue)
        {
            List<AchievementCardModel> earnedAchievements = new List<AchievementCardModel>();

            if(Evaluations is null || Evaluations.Count == 0) return earnedAchievements;

            foreach (var eval in Evaluations)
            {
                double prog = 0;

                if(eval is ValueAchievementEvaluation veval)
                {
                    prog = veval.IncrementAndGetValue(additionalValue);
                }
                else if(eval is TimeAchievementEvaluation teval)
                {
                    prog = teval.IncrementAndGetValue(additionalTime);
                }

                if(prog >= 1)
                {
                    earnedAchievements.Add(eval.AchievemenCard);
                }
            }

            return earnedAchievements;
        }
    }

    public interface ITimeValueAchievementEvaluation
    {
        public AchievementCardModel AchievemenCard { get; set; }
        public double IncrementAndGetValue(double incrementBy);
    }

    public class ValueAchievementEvaluation : ITimeValueAchievementEvaluation
    {
        public AchievementCardModel AchievemenCard {  get; set; } 

        public double CurrentValue { get; set; }

        public double IncrementAndGetValue(double incrementBy)
        {
            CurrentValue += incrementBy;

            return CurrentValue;
        }
    }

    public class TimeAchievementEvaluation : ITimeValueAchievementEvaluation
    {
        public AchievementCardModel AchievemenCard { get; set; }

        public double CurrentTotalSeconds { get; set; }

        public double IncrementAndGetValue(double incrementBy)
        {
            CurrentTotalSeconds += incrementBy;

            return CurrentTotalSeconds;
        }
    }
}
