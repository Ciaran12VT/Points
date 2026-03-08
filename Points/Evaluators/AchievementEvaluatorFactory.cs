using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Evaluators
{
    public static class AchievementEvaluatorFactory
    {
        public static Dictionary<string, TimeValueAchievementEvaluator> BuildEvaluatorsByTag(
            IEnumerable<AchievementCardModel> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));

            return cards
                // group by the Tags string (normalise null as empty string if you like)
                .GroupBy(c => c.Tags ?? string.Empty)
                .ToDictionary(
                    g => g.Key,
                    g => new TimeValueAchievementEvaluator
                    {
                        Evaluations = g
                            .Select(card => CreateEvaluation(card))
                            .ToList()
                    });
        }

        private static TimeValueAchievementEvaluation CreateEvaluation(AchievementCardModel card)
        {
            return card.GoalType switch
            {
                AchievementGoalType.ActiveTime => new TimeValueAchievementEvaluation
                {
                    AchievementCard = card,
                },
                AchievementGoalType.Value => new TimeValueAchievementEvaluation
                {
                    AchievementCard = card
                },
                _ => throw new NotSupportedException(
                    $"Unsupported GoalType '{card.GoalType}' for AchievementCard '{card}'.")
            };
        }
    }

}
