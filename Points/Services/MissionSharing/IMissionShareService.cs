using Points.Models;

namespace Points.Services.MissionSharing;

public interface IMissionShareService
{
    Task ShareMissionAsync(MissionCardModel mission);
    Task<MissionSharePreview> CreateImportPreviewAsync(string filePath);
    Task<MissionCardModel> AcceptImportAsync(MissionSharePreview preview);
}
