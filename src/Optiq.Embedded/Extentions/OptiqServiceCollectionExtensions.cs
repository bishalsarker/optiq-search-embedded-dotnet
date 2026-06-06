using Microsoft.Extensions.DependencyInjection;
using Optiq.Embedded.Configurations;
using Optiq.Embedded.Interfaces;
using Optiq.Embedded.Persistence;

namespace Optiq.Embedded.Extentions
{
    public static class OptiqServiceCollectionExtensions
    {
        public static IServiceCollection AddOptiq(this IServiceCollection services, Action<OptiqOptions> configure)
        {
            var options = new OptiqOptions();
            configure(options);

            services.AddSingleton(options);
            services.AddSingleton<IOptiqContext>(sp => new RocksDbContext(options));

            return services;
        }
    }
}
