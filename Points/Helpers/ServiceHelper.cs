using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Helpers
{
    public static class ServiceHelper
    {
        public static IServiceProvider Services { get; set; } = default!;

        public static T GetService<T>() where T : notnull
            => (T)Services.GetService(typeof(T))!;
    }
}
