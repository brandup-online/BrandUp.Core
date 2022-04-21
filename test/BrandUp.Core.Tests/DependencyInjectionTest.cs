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

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var validator = scope.ServiceProvider.GetRequiredService<IValidator>();
            var userProvider = scope.ServiceProvider.GetRequiredService<UserProvider>();
            var userProvider2 = scope.ServiceProvider.GetRequiredService<Items.IItemProvider<Guid, User>>();

            Assert.NotNull(domain);
            Assert.NotNull(validator);
            Assert.NotNull(userProvider);
            Assert.NotNull(userProvider2);
        }

        [Fact]
        public void RequireScope()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
            {
                options.AddCommand<Example.Commands.JoinUserCommandHandler>();
            })
                .AddItemProvider<UserProvider>()
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<IDomain>());
        }
    }
}