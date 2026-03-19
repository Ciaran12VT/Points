using Points.Services.Sqlite.Services.Interfaces;

namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface IHomeSeedReadRepository
    {
        Task<HomeSeedData> GetHomeSeedDataAsync(DateTime rangeStart, DateTime rangeEnd);
    }

}