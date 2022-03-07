using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp.CQRS
{
    public class DependencyInjectionTest
    {
        [Fact]
        public async void Test1()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddCQRS(builder =>
            {
                builder.AddCommand<Example.Commands.JoinUserCommandHandler>();
            });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var joinUserResult = await domain.SendAsync(new Example.Commands.JoinUserCommand());
        }
    }
}