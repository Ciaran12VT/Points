using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services.Sqlite.Services.Interfaces
{
    public interface ICardReadService
    {
        Task<List<AchievementCardModel>> GetAchievementCardModelsDataAsync();
        Task<List<TrophyModel>> GetTrophyModelsDataAsync();

        Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd);
        Task<List<IActiveCardModel>> GetMainQuestModelsDataAsync(DateTime rangeStart, DateTime rangeEnd);

        Task<CardSchedule?> GetCardScheduleByIdAsync(long scheduleId);
        Task<string?> GetCardTitleByIdAsync(long cardId);
    }
}
