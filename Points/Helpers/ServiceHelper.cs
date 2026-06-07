using Microsoft.Extensions.DependencyInjection;

namespace Points.Helpers
{
    public static class ServiceHelper
    {
        private static IServiceProvider? _services;

        public static IServiceProvider Services
        {
            get => _services ?? throw new InvalidOperationException("Application services have not been initialized.");
            set => _services = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static T GetService<T>() where T : notnull
            => Services.GetRequiredService<T>();
    }
}
