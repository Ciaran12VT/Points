using Points.Services.Sqlite.Providers.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services.Sqlite.Providers.Interfaces
{
    public interface IPointsSchemaProvider
    {
        DatabaseSchemaDefinition GetSchema();
    }
}
