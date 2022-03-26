using BrandUp.Example.Items;
using BrandUp.Example.Queries;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Xunit;

namespace BrandUp
{
    public class DomainTest
    {
        [Fact]
        public void GetItemProvider()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => { })
                .AddItemProvider<UserProvider>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var userProvider = domain.GetItemProvider<UserProvider>();

            Assert.NotNull(userProvider);
        }

        [Fact]
        public async void FindItemAsync()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => { })
                .AddItemProvider<UserProvider>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var item = await domain.FindItem<Guid, User>(Guid.Empty);

            Assert.NotNull(item);
        }

        [Fact]
        public async void QueryAsync()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
            {
                options.AddQuery<UserByPhoneQueryHandler>();
            })
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var userByPhoneResult = await domain.QueryAsync(new UserByPhoneQuery { Phone = "89232229022" });

            Assert.True(userByPhoneResult.IsSuccess);
            Assert.NotNull(userByPhoneResult.Data);
            Assert.Single(userByPhoneResult.Data);
            Assert.Equal("89232229022", userByPhoneResult.Data.Single().Phone);
        }

        [Fact]
        public async void SendItemAsync_ById_NotResult()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
            {
                options.AddCommand<Example.Commands.VisitUserCommandHandler>();
            })
                .AddItemProvider<UserProvider>()
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var user = new User { Id = Guid.NewGuid(), Phone = "89232229022" };

            var joinUserResult = await domain.SendItemAsync(Guid.Empty, new Example.Commands.VisitUserCommand());

            Assert.True(joinUserResult.IsSuccess);
        }

        [Fact]
        public async void SendItemAsync_ByItem_NotResult()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.VisitUserCommandHandler>();
                })
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var user = new User { Id = Guid.NewGuid(), Phone = "89232229022" };

            var joinUserResult = await domain.SendItemAsync(user, new Example.Commands.VisitUserCommand());

            Assert.True(joinUserResult.IsSuccess);
        }

        [Fact]
        public async void SendAsync_WithResult()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.JoinUserCommandHandler>();
                })
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var joinUserResult = await domain.SendAsync(new Example.Commands.JoinUserCommand { Phone = "+79231145449" });

            Assert.True(joinUserResult.IsSuccess);
            Assert.NotNull(joinUserResult.Data.User);
            Assert.Equal("+79231145449", joinUserResult.Data.User.Phone);
        }

        [Fact]
        public async void SendAsync_CommandInvalid()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
            {
                options.AddCommand<Example.Commands.JoinUserCommandHandler>();
            })
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var joinUserResult = await domain.SendAsync(new Example.Commands.JoinUserCommand());

            Assert.False(joinUserResult.IsSuccess);
        }
    }
}