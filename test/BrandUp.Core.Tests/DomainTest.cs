using System;
using System.Linq;
using System.Threading.Tasks;
using BrandUp.Example.Items;
using BrandUp.Example.Queries;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
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
        public async Task FindItemAsync()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => { })
                .AddItemProvider<UserProvider>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var item = await domain.FindItemAsync<Guid, User>(
                Guid.Empty,
                TestContext.Current.CancellationToken);

            Assert.NotNull(item);
        }

        [Fact]
        public async Task QueryAsync()
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

            var userByPhoneResult = await domain.QueryAsync(
                new UserByPhoneQuery { Phone = "89232229022" },
                TestContext.Current.CancellationToken);

            Assert.True(userByPhoneResult.IsSuccess);
            Assert.NotNull(userByPhoneResult.Data);
            Assert.Single(userByPhoneResult.Data);
            Assert.Equal("89232229022", userByPhoneResult.Data.Single().Phone);
        }

        [Fact]
        public async Task SendItemAsync_ById_NotResult()
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

            var joinUserResult = await domain.SendItemAsync(Guid.Empty,
                new Example.Commands.VisitUserCommand(),
                TestContext.Current.CancellationToken);

            Assert.True(joinUserResult.IsSuccess);
        }

        [Fact]
        public async Task SendItemAsync_ByItem_NotResult()
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

            var joinUserResult = await domain.SendItemAsync(user,
                new Example.Commands.VisitUserCommand(),
                TestContext.Current.CancellationToken);

            Assert.True(joinUserResult.IsSuccess);
        }

        [Fact]
        public async Task SendItemAsync_ByItem_WithResult()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.RenameUserCommandHandler>();
                })
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var user = new User { Id = Guid.NewGuid(), Phone = "89232229022" };

            var result = await domain.SendItemAsync<Guid, User, string>(user,
                new Example.Commands.RenameUserCommand { NewPhone = "+79231145449" },
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal("+79231145449", result.Data);
            Assert.Equal("+79231145449", user.Phone);
        }

        [Fact]
        public async Task SendItemAsync_ById_WithResult()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.RenameUserCommandHandler>();
                })
                .AddItemProvider<UserProvider>()
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendItemAsync<Guid, User, string>(Guid.NewGuid(),
                new Example.Commands.RenameUserCommand { NewPhone = "+79231145449" },
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal("+79231145449", result.Data);
        }

        [Fact]
        public async Task QueryAsync_HandlerNotRegistered_Throws()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => { });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                domain.QueryAsync(
                    new UserByPhoneQuery { Phone = "89232229022" },
                    TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SendAsync_WithResult()
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

            var joinUserResult = await domain.SendAsync(
                new Example.Commands.JoinUserCommand { Phone = "+79231145449" },
                TestContext.Current.CancellationToken);

            Assert.True(joinUserResult.IsSuccess);
            Assert.NotNull(joinUserResult.Data.User);
            Assert.Equal("+79231145449", joinUserResult.Data.User.Phone);
        }

        [Fact]
        public async Task SendAsync_HandlerResolvedFromDI()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton<Example.Commands.ICounter, Example.Commands.Counter>();
            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.CounterCommandHandler>();
                })
                .AddValidator<ComponentModelValidator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendAsync(
                new Example.Commands.CounterCommand(),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Data);
        }

        [Fact]
        public async Task SendAsync_HandlerException_PropagatesOriginal()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.FailingCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                domain.SendAsync(
                    new Example.Commands.FailingCommand(),
                    TestContext.Current.CancellationToken));

            Assert.Equal("boom", ex.Message);
        }

        [Fact]
        public async Task SendAsync_CommandInvalid()
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

            var joinUserResult = await domain.SendAsync(
                new Example.Commands.JoinUserCommand(),
                TestContext.Current.CancellationToken);

            Assert.False(joinUserResult.IsSuccess);
        }
    }
}