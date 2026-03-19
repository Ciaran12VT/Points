using Points.Evaluators;
using Points.Models;
using Points.Services.Sqlite.Repositories.Classes;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IAchievementEvaluationService
    {
        Task<TimeValueAchievementEvaluation> CreateEvaluationAsync(AchievementCardModel card);

        Task<TagValueSummaryModel> GetTagValueSummaryAsync(
            string tagName,
            DateTime rangeStart,
            DateTime rangeEnd);
    }
}