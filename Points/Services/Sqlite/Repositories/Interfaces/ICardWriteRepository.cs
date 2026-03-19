using Points.Models;
namespace Points.Services.Sqlite.Repositories.Interfaces
{
    public interface ICardWriteRepository
    {
        Task SaveCardModelAsync(ICardModel model);
    }

}