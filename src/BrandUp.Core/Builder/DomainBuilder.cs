using Microsoft.Extensions.DependencyInjection;

namespace BrandUp.Builder
{
    public class DomainBuilder : IDomainBuilder
    {
        public IServiceCollection Services { get; }

        public DomainBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));

            AddCoreServices();
        }

        internal void AddCoreServices()
        {
            var services = Services;

            services.AddScoped<IDomain, Domain>();
        }
    }

    public interface IDomainBuilder
    {
        IServiceCollection Services { get; }
    }
}