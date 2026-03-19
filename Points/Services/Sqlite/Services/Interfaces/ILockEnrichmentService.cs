using Points.Models;

namespace Points.Services.Sqlite.Services.Interfaces
{
    public interface ILockEnrichmentService
    {
        Task PopulateLocksAsync(
            List<IActiveCardModel> mainQuestCards,
            List<MissionCardModel> missionCards);
    }
}