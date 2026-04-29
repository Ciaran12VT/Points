namespace Points.Models;

using Points.Evaluators;

public interface IActiveCardModel : ICardModel
{
    List<TimeValueAchievementEvaluator> TimeValueAchievementEvaluators { get; set; }
    List<LockModel> Locks { get; set; }
}
