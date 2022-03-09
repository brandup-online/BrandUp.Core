using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class DependencyInjectionTest
    {
        [Fact]
        public void GetService_Check()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddCQRS(options =>
            {
                options.AddCommand<Example.Commands.JoinUserCommandHandler>();
            });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetService<IDomain>();

            Assert.NotNull(domain);
        }
    }
}