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
        public async Task SendAsync_CommandsSharingResultType_DispatchByCommandType()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton<Example.Commands.ICounter, Example.Commands.Counter>();
            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.CounterCommandHandler>();
                    options.AddCommand<Example.Commands.SquareCounterCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var counter = await domain.SendAsync(
                new Example.Commands.CounterCommand(),
                TestContext.Current.CancellationToken);
            var square = await domain.SendAsync(
                new Example.Commands.SquareCounterCommand(),
                TestContext.Current.CancellationToken);

            Assert.Equal(42, counter.Data);
            Assert.Equal(42 * 42, square.Data);
        }

        [Fact]
        public async Task SendAsync_NonGeneric_OnCommandWithResult_Throws()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton<Example.Commands.ICounter, Example.Commands.Counter>();
            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.CounterCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                domain.SendAsync(
                    (Commands.ICommand)new Example.Commands.CounterCommand(),
                    TestContext.Current.CancellationToken));
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

        [Fact]
        public async Task SendAsync_RunsAllValidators_AggregatesErrors()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton<Example.Commands.ICounter, Example.Commands.Counter>();
            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.CounterCommandHandler>();
                })
                .AddValidator<Example.Validation.FailingValidatorA>()
                .AddValidator<Example.Validation.FailingValidatorB>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendAsync(
                new Example.Commands.CounterCommand(),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(2, result.CountErrors);
        }

        [Fact]
        public async Task SendAsync_NotResult_Success()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.PingCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendAsync(
                new Example.Commands.PingCommand(),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task SendAsync_Generic_OnCommandWithoutResult_Throws()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.MixedCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                domain.SendAsync(
                    new Example.Commands.MixedCommand(),
                    TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SendItemAsync_ById_NotFound_ReturnsError()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.VisitUserCommandHandler>();
                })
                .AddItemProvider<NullUserProvider>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendItemAsync<Guid, User>(Guid.NewGuid(),
                new Example.Commands.VisitUserCommand(),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task SendItemAsync_ById_WithResult_NotFound_ReturnsError()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.RenameUserCommandHandler>();
                })
                .AddItemProvider<NullUserProvider>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendItemAsync<Guid, User, string>(Guid.NewGuid(),
                new Example.Commands.RenameUserCommand { NewPhone = "+79231145449" },
                TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task SendItemAsync_NotResult_OnCommandWithResult_Throws()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.RenameUserCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var user = new User { Id = Guid.NewGuid(), Phone = "89232229022" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                domain.SendItemAsync<Guid, User>(user,
                    (Commands.IItemCommand<User>)new Example.Commands.RenameUserCommand { NewPhone = "x" },
                    TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SendItemAsync_WithResult_OnCommandWithoutResult_Throws()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.TouchUserCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var user = new User { Id = Guid.NewGuid(), Phone = "89232229022" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                domain.SendItemAsync<Guid, User, string>(user,
                    new Example.Commands.TouchUserCommand(),
                    TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task QueryAsync_Invalid_ReturnsError()
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

            var result = await domain.QueryAsync(
                new UserByPhoneQuery(),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task SendAsync_DisposesHandler()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            var probe = new Example.Commands.DisposeProbe();
            serviceCollection.AddSingleton(probe);
            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.DisposableCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendAsync(
                new Example.Commands.DisposableCommand(),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.True(probe.Disposed);
        }

        [Fact]
        public async Task SendAsync_DisposesAsyncHandler()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            var probe = new Example.Commands.DisposeProbe();
            serviceCollection.AddSingleton(probe);
            serviceCollection.AddDomain(options =>
                {
                    options.AddCommand<Example.Commands.AsyncDisposableCommandHandler>();
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            var result = await domain.SendAsync(
                new Example.Commands.AsyncDisposableCommand(),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.True(probe.AsyncDisposed);
        }

        [Fact]
        public void GetItemProvider_NotRegistered_Throws()
        {
            #region Prepare

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(options => { });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            using var scope = serviceProvider.CreateAsyncScope();

            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            #endregion

            Assert.Throws<InvalidOperationException>(() => domain.GetItemProvider<UserProvider>());
        }
    }
}