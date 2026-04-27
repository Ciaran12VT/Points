using Points.Models;

namespace Points.Services.Sqlite.Interfaces;

public interface IUdmdService
{
    Task<List<UdmdConfigModel>> GetUdmdConfigsForCardAsync(long cardId);
    Task<List<UdmdConfigModel>> GetActiveUdmdConfigsForCardAsync(long cardId);
    Task<UdmdConfigModel> SaveUdmdConfigAsync(UdmdConfigModel config);
    Task DeleteOrDeactivateUdmdConfigAsync(long udmdConfigId);
    Task<List<UdmdDropdownModel>> GetDropdownValuesAsync(long udmdConfigId);
    Task SaveDropdownValuesAsync(long udmdConfigId, IEnumerable<string> values);

    Task SaveMetadataForEntityAsync(
        long cardId,
        string relatedEntityType,
        long relatedEntityId,
        IEnumerable<UdmdValueInput> values);

    Task<List<UdmdTransModel>> GetMetadataForEntityAsync(
        string relatedEntityType,
        long relatedEntityId);

    Task<List<UdmdTransModel>> GetMetadataForCardAsync(long cardId);

    Task SaveActivityMetadataAsync(long cardId, long activityId, IEnumerable<UdmdValueInput> values);
    Task SaveBudgetTransactionMetadataAsync(long cardId, long budgetTransactionId, IEnumerable<UdmdValueInput> values);
    Task SaveTrackerValueMetadataAsync(long cardId, long trackerValueId, IEnumerable<UdmdValueInput> values);
}
