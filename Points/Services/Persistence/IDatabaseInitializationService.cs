using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services.Persistence
{
    public interface IDatabaseInitializationService
    {
        Task InitializeAsync();
        Task CloseDatabaseAsync();
        Task ReinitializeDatabaseAsync();
    }
}
