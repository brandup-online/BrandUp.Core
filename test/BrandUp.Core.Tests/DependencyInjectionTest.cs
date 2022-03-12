using BrandUp.Example.Items;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace BrandUp
{
    public class DependencyInjectionTest
    {
        [Fact]
        public void GetService_Check()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.JoinUserCommandHandler>();
                })
                .AddItemProvider<UserProvider>()
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetService<IDomain>();
            var validator = scope.ServiceProvider.GetService<IValidator>();
            var userProvider = scope.ServiceProvider.GetService<UserProvider>();
            var userProvider2 = scope.ServiceProvider.GetService<Items.IItemProvider<Guid, User>>();

            Assert.NotNull(domain);
            Assert.NotNull(validator);
            Assert.NotNull(userProvider);
            Assert.NotNull(userProvider2);
        }
    }
}