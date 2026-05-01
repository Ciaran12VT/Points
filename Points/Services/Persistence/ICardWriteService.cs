using Points.Evaluators;
using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Points.Services.Persistence
{
    public interface ICardWriteService
    {
        Task SaveCardModelAsync(ICardModel model);
        Task<bool> WouldArchiveCardModelOnDeleteAsync(ICardModel model);
        Task DeleteCardModelAsync(ICardModel model);
    }
}
