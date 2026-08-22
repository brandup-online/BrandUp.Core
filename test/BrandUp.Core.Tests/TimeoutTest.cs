using System;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Behaviors;
using BrandUp.Commands;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class TimeoutTest
    {
        static ServiceProvider BuildServices(Action<DomainOptions> configure)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDomain(configure)
                .AddDispatchTimeouts();

            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task TimedOut_ReturnsTimeoutError()
        {
            using var serviceProvider = BuildServices(options => options.AddCommand<SlowCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new SlowCommand(), TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            var error = Assert.Single(result.Errors);
            Assert.Equal(DomainErrors.DispatchTimeout.Code, error.Code);
            Assert.Contains(nameof(SlowCommand), error.Message);
        }

        [Fact]
        public async Task WithinTimeout_Succeeds()
        {
            using var serviceProvider = BuildServices(options => options.AddCommand<FastCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new FastCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CallerCancellation_StillThrows()
        {
            using var serviceProvider = BuildServices(options => options.AddCommand<SlowCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            // The caller's own cancellation is not a timeout - it must not become an error result.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => domain.SendAsync(new SlowCommand(), cancellation.Token));
        }

        public class SlowCommand : ICommand, IDispatchTimeout
        {
            public TimeSpan Timeout => TimeSpan.FromMilliseconds(200);
        }

        public class SlowCommandHandler : ICommandHandler<SlowCommand>
        {
            public async Task<Result> HandleAsync(SlowCommand command, CancellationToken cancellationToken = default)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return Result.Success();
            }
        }

        public class FastCommand : ICommand, IDispatchTimeout
        {
            public TimeSpan Timeout => TimeSpan.FromSeconds(30);
        }

        public class FastCommandHandler : ICommandHandler<FastCommand>
        {
            public Task<Result> HandleAsync(FastCommand command, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result.Success());
            }
        }
    }
}
