using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Authorization;
using BrandUp.Behaviors;
using BrandUp.Commands;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class AuthorizationTest
    {
        static ServiceProvider BuildServices(Action<DomainOptions> configure, Action<IDomainBuilder> configureBuilder)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ExecutionCounter>();

            var builder = serviceCollection.AddDomain(configure);
            configureBuilder(builder);

            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task Denied_ShortCircuitsBeforeHandler()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<ProtectedCommandHandler>(),
                builder => builder
                    .AddAuthorization()
                    .AddAuthorizer<ProtectedCommandAuthorizer>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new ProtectedCommand { Allow = false }, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            var error = Assert.Single(result.Errors);
            Assert.Equal(DomainErrors.AccessDenied.Code, error.Code);
            Assert.Equal(ErrorKind.Forbidden, error.Kind);
            Assert.Equal(0, serviceProvider.GetRequiredService<ExecutionCounter>().Value);
        }

        [Fact]
        public async Task Allowed_ReachesHandler()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<ProtectedCommandHandler>(),
                builder => builder
                    .AddAuthorization()
                    .AddAuthorizer<ProtectedCommandAuthorizer>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new ProtectedCommand { Allow = true }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, serviceProvider.GetRequiredService<ExecutionCounter>().Value);
        }

        [Fact]
        public async Task Denied_TypedCommand_KeepsResultShape()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<ProtectedResultCommandHandler>(),
                builder => builder
                    .AddAuthorization()
                    .AddAuthorizer<DenyingResultAuthorizer>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            // The authorizer returns a plain Result; the dispatch must still produce Result<int>.
            Result<int> result = await domain.SendAsync(new ProtectedResultCommand(), TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(DomainErrors.AccessDenied.Code, result.Errors.Single().Code);
        }

        [Fact]
        public async Task NullReturningAuthorizer_ThrowsDescriptively()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<ProtectedCommandHandler>(),
                builder => builder
                    .AddAuthorization()
                    .AddAuthorizer<NullAuthorizer>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => domain.SendAsync(new ProtectedCommand { Allow = true }, TestContext.Current.CancellationToken));
            Assert.Contains(nameof(NullAuthorizer), exception.Message);
        }

        [Fact]
        public void AddAuthorization_AfterQueryCaching_Throws()
        {
            var serviceCollection = new ServiceCollection();
            var builder = serviceCollection.AddDomain().AddQueryCaching();

            Assert.Throws<InvalidOperationException>(() => builder.AddAuthorization());
        }

        [Fact]
        public void AddAuthorizer_WithoutAuthorizerInterface_Throws()
        {
            var serviceCollection = new ServiceCollection();
            var builder = serviceCollection.AddDomain();

            Assert.Throws<InvalidOperationException>(() => builder.AddAuthorizer<ExecutionCounter>());
        }

        public class ExecutionCounter
        {
            int value;

            public int Value => value;

            public void Increment() => Interlocked.Increment(ref value);
        }

        public class ProtectedCommand : ICommand
        {
            public bool Allow { get; set; }
        }

        public class ProtectedCommandHandler(ExecutionCounter counter) : ICommandHandler<ProtectedCommand>
        {
            public Task<Result> HandleAsync(ProtectedCommand command, CancellationToken cancellationToken = default)
            {
                counter.Increment();
                return Task.FromResult(Result.Success());
            }
        }

        public class ProtectedCommandAuthorizer : IDomainAuthorizer<ProtectedCommand>
        {
            public Task<Result> AuthorizeAsync(ProtectedCommand request, DomainBehaviorContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(request.Allow ? Result.Success() : Result.Error(DomainErrors.AccessDenied));
            }
        }

        public class ProtectedResultCommand : ICommand<int>
        {
        }

        public class ProtectedResultCommandHandler : ICommandHandler<ProtectedResultCommand, int>
        {
            public Task<Result<int>> HandleAsync(ProtectedResultCommand command, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result.Success(42));
            }
        }

        public class NullAuthorizer : IDomainAuthorizer<ProtectedCommand>
        {
            public Task<Result> AuthorizeAsync(ProtectedCommand request, DomainBehaviorContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<Result>(null);
            }
        }

        public class DenyingResultAuthorizer : IDomainAuthorizer<ProtectedResultCommand>
        {
            public Task<Result> AuthorizeAsync(ProtectedResultCommand request, DomainBehaviorContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result.Error(DomainErrors.AccessDenied));
            }
        }
    }
}
