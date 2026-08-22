using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Commands;
using BrandUp.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class IdempotencyTest
    {
        static ServiceProvider BuildServices(Action<DomainOptions> configure)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<HandlerGate>();

            serviceCollection.AddDomain(configure)
                .AddIdempotency();

            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task RepeatedKey_ReplaysStoredResult()
        {
            using var serviceProvider = BuildServices(options => options.AddCommand<PayCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var gate = serviceProvider.GetRequiredService<HandlerGate>();

            var first = await domain.SendAsync(new PayCommand { Key = "replay" }, TestContext.Current.CancellationToken);
            var second = await domain.SendAsync(new PayCommand { Key = "replay" }, TestContext.Current.CancellationToken);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(first.Data, second.Data);
            Assert.Equal(1, gate.Entered);
        }

        [Fact]
        public async Task DifferentKeys_ExecuteIndependently()
        {
            using var serviceProvider = BuildServices(options => options.AddCommand<PayCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var gate = serviceProvider.GetRequiredService<HandlerGate>();

            await domain.SendAsync(new PayCommand { Key = "first" }, TestContext.Current.CancellationToken);
            await domain.SendAsync(new PayCommand { Key = "second" }, TestContext.Current.CancellationToken);

            Assert.Equal(2, gate.Entered);
        }

        [Fact]
        public async Task FailedCommand_ReleasesKeyForRetry()
        {
            using var serviceProvider = BuildServices(options => options.AddCommand<PayCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var gate = serviceProvider.GetRequiredService<HandlerGate>();

            var failed = await domain.SendAsync(new PayCommand { Key = "retry", Fail = true }, TestContext.Current.CancellationToken);
            var retried = await domain.SendAsync(new PayCommand { Key = "retry" }, TestContext.Current.CancellationToken);

            Assert.False(failed.IsSuccess);
            Assert.True(retried.IsSuccess);
            Assert.Equal(2, gate.Entered);
        }

        [Fact]
        public async Task InFlightKey_ReportsDuplicate()
        {
            using var serviceProvider = BuildServices(options => options.AddCommand<PayCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var gate = serviceProvider.GetRequiredService<HandlerGate>();

            var firstDispatch = domain.SendAsync(new PayCommand { Key = "inflight", WaitGate = true }, TestContext.Current.CancellationToken);
            while (Volatile.Read(ref gate.Entered) == 0)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            var duplicate = await domain.SendAsync(new PayCommand { Key = "inflight" }, TestContext.Current.CancellationToken);

            Assert.False(duplicate.IsSuccess);
            var error = Assert.Single(duplicate.Errors);
            Assert.Equal(DomainErrors.DuplicateRequest.Code, error.Code);
            Assert.Equal(ErrorKind.Conflict, error.Kind);

            gate.Release();
            var first = await firstDispatch;
            Assert.True(first.IsSuccess);
        }

        [Fact]
        public async Task KeyReusedAcrossCommandTypes_ReportsDuplicate()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PayCommandHandler>();
                options.AddCommand<PlainPayCommandHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var gate = serviceProvider.GetRequiredService<HandlerGate>();

            var completed = await domain.SendAsync(new PayCommand { Key = "foreign" }, TestContext.Current.CancellationToken);
            Assert.True(completed.IsSuccess);

            // A different command type must not replay a foreign command's stored result.
            var duplicate = await domain.SendAsync(new PlainPayCommand { Key = "foreign" }, TestContext.Current.CancellationToken);

            Assert.False(duplicate.IsSuccess);
            Assert.Equal(DomainErrors.DuplicateRequest.Code, duplicate.Errors.Single().Code);
            Assert.Equal(1, gate.Entered);
        }

        [Fact]
        public async Task AbandonedClaim_ExpiresWithLease()
        {
            var timeProvider = new TestTimeProvider();
            var store = new InMemoryIdempotencyStore(claimLease: TimeSpan.FromMinutes(5), timeProvider: timeProvider);

            Assert.Null(await store.TryClaimAsync("stuck", TestContext.Current.CancellationToken));

            var blocked = await store.TryClaimAsync("stuck", TestContext.Current.CancellationToken);
            Assert.NotNull(blocked);
            Assert.False(blocked.IsCompleted);

            // The claim was abandoned (crash, lost completion): the lease frees the key.
            timeProvider.UtcNow += TimeSpan.FromMinutes(6);
            Assert.Null(await store.TryClaimAsync("stuck", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task NestedCommand_IsExemptFromDeduplication()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PayCommandHandler>();
                options.AddCommand<OuterCommandHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var gate = serviceProvider.GetRequiredService<HandlerGate>();

            var result = await domain.SendAsync(new OuterCommand(), TestContext.Current.CancellationToken);

            // The outermost dispatch owns deduplication: both nested dispatches executed.
            Assert.True(result.IsSuccess);
            Assert.Equal(2, gate.Entered);
        }

        sealed class TestTimeProvider : TimeProvider
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

            public override DateTimeOffset GetUtcNow() => UtcNow;
        }

        public class HandlerGate
        {
            readonly TaskCompletionSource source = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int Entered;

            public Task Wait(CancellationToken cancellationToken) => source.Task.WaitAsync(cancellationToken);

            public void Release() => source.TrySetResult();
        }

        public class PayCommand : ICommand<int>, IIdempotentCommand
        {
            public string Key { get; set; }
            public bool Fail { get; set; }
            public bool WaitGate { get; set; }

            public string IdempotencyKey => Key;
        }

        public class PayCommandHandler(HandlerGate gate) : ICommandHandler<PayCommand, int>
        {
            public async Task<Result<int>> HandleAsync(PayCommand command, CancellationToken cancellationToken = default)
            {
                var value = Interlocked.Increment(ref gate.Entered);

                if (command.WaitGate)
                    await gate.Wait(cancellationToken);

                if (command.Fail)
                    return Result.Error<int>("pay-failed", "Payment failed.");

                return Result.Success(value);
            }
        }

        public class PlainPayCommand : ICommand, IIdempotentCommand
        {
            public string Key { get; set; }

            public string IdempotencyKey => Key;
        }

        public class PlainPayCommandHandler(HandlerGate gate) : ICommandHandler<PlainPayCommand>
        {
            public Task<Result> HandleAsync(PlainPayCommand command, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref gate.Entered);
                return Task.FromResult(Result.Success());
            }
        }

        public class OuterCommand : ICommand
        {
        }

        public class OuterCommandHandler(IDomain domain) : ICommandHandler<OuterCommand>
        {
            public async Task<Result> HandleAsync(OuterCommand command, CancellationToken cancellationToken = default)
            {
                var first = await domain.SendAsync(new PayCommand { Key = "nested" }, cancellationToken);
                if (!first.IsSuccess)
                    return Result.Error(first.Errors);

                var second = await domain.SendAsync(new PayCommand { Key = "nested" }, cancellationToken);
                if (!second.IsSuccess)
                    return Result.Error(second.Errors);

                return Result.Success();
            }
        }
    }
}
