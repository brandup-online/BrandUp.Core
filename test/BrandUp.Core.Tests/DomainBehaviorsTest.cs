using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Behaviors;
using BrandUp.Caching;
using BrandUp.Commands;
using BrandUp.Events;
using BrandUp.Example.Behaviors;
using BrandUp.Example.Commands;
using BrandUp.Example.Events;
using BrandUp.Example.Queries;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EventLog = BrandUp.Example.Events.EventLog;

namespace BrandUp
{
    public class DomainBehaviorsTest
    {
        static ServiceProvider BuildServices(Action<DomainOptions> configure, Action<IDomainBuilder> configureBuilder = null)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<EventLog>();
            serviceCollection.AddSingleton<DisposeProbe>();
            serviceCollection.AddSingleton<ContextProbe>();

            var builder = serviceCollection.AddDomain(configure);
            configureBuilder?.Invoke(builder);

            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task Behaviors_RunInRegistrationOrder_AroundHandler()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<PublishingCommandHandler>(),
                builder => builder
                    .AddBehavior<FirstLoggingBehavior>()
                    .AddBehavior<SecondLoggingBehavior>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["first:before", "second:before", "command", "second:after", "first:after"], log.Entries);
        }

        [Fact]
        public async Task Behavior_ShortCircuits_WithTypedError()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<PublishingCommandHandler>(),
                builder => builder.AddBehavior<ShortCircuitBehavior>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            var error = Assert.Single(result.Errors);
            Assert.Equal("blocked", error.Code);
            Assert.Equal(ErrorKind.Forbidden, error.Kind);
            Assert.Empty(log.Entries); // handler never ran
        }

        [Fact]
        public async Task Behavior_ShortCircuits_TypedQueryError()
        {
            using var serviceProvider = BuildServices(
                options => options.AddQuery<UserCountQueryHandler>(),
                builder => builder.AddBehavior<ShortCircuitBehavior>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            // CreateError must produce Result<int>, not a bare Result.
            var result = await domain.QueryAsync(new UserCountQuery(), TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("blocked", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public async Task Transactions_CommitOnSuccess_DeferredFlushAfterCommit()
        {
            using var serviceProvider = BuildServices(
                options =>
                {
                    options.AddCommand<PublishingCommandHandler>();
                    options.AddEvent<UserJoinedDeferredHandler>();
                },
                builder => builder.AddTransactions<FakeTransactionFactory>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            // The strict guarantee: deferred handlers run only after the commit.
            Assert.Equal(["tx-begin", "command", "tx-commit", "deferred:+1"], log.Entries);
        }

        [Fact]
        public async Task Transactions_AbortOnErrorResult()
        {
            using var serviceProvider = BuildServices(
                options =>
                {
                    options.AddCommand<PublishingCommandHandler>();
                    options.AddEvent<UserJoinedDeferredHandler>();
                },
                builder => builder.AddTransactions<FakeTransactionFactory>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1", Fail = true }, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(["tx-begin", "command", "tx-abort"], log.Entries);
        }

        [Fact]
        public async Task Transactions_AbortOnException()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<PublishingCommandHandler>(),
                builder => builder.AddTransactions<FakeTransactionFactory>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => domain.SendAsync(new PublishingCommand { Phone = "+1", Throw = true }, TestContext.Current.CancellationToken));

            Assert.Equal(["tx-begin", "command", "tx-abort"], log.Entries);
        }

        [Fact]
        public async Task Transactions_SkipNonTransactionalCommand()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<ManualTxCommandHandler>(),
                builder => builder.AddTransactions<FakeTransactionFactory>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new ManualTxCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["manual-command"], log.Entries);
        }

        [Fact]
        public async Task Transactions_SkipQueries()
        {
            using var serviceProvider = BuildServices(
                options => options.AddQuery<UserCountQueryHandler>(),
                builder => builder.AddTransactions<FakeTransactionFactory>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.QueryAsync(new UserCountQuery(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain("tx-begin", log.Entries);
        }

        [Fact]
        public async Task QueryCache_ServesRepeatedQueryFromCache()
        {
            using var serviceProvider = BuildServices(
                options => options.AddQuery<CachedCountQueryHandler>(),
                builder => builder.AddQueryCaching());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var first = await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);
            var second = await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);

            Assert.Equal(42, first.Data);
            Assert.Equal(42, second.Data);
            Assert.Single(log.Entries, "cached-query-exec");
        }

        [Fact]
        public async Task QueryCache_InvalidatedByCommand()
        {
            using var serviceProvider = BuildServices(
                options =>
                {
                    options.AddQuery<CachedCountQueryHandler>();
                    options.AddCommand<InvalidateCountCommandHandler>();
                },
                builder => builder.AddQueryCaching());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);
            await domain.SendAsync(new InvalidateCountCommand(), TestContext.Current.CancellationToken);
            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);

            Assert.Equal(2, log.Entries.Count(entry => entry == "cached-query-exec"));
        }

        [Fact]
        public async Task QueryCache_InvalidationRunsAfterCommit_RegardlessOfRegistrationOrder()
        {
            // Deliberately the "wrong" order: transactions first, caching second. AddQueryCaching
            // must still keep the cache behavior outside TransactionBehavior.
            using var serviceProvider = BuildServices(
                options => options.AddCommand<InvalidateCountCommandHandler>(),
                builder => builder
                    .AddTransactions<FakeTransactionFactory>()
                    .AddQueryCaching<RecordingQueryCache>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new InvalidateCountCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var entries = log.Entries.ToList();
            Assert.True(entries.IndexOf("tx-commit") >= 0 && entries.IndexOf("cache-remove:user-count") > entries.IndexOf("tx-commit"),
                $"Expected invalidation after commit, but the log is: {string.Join(", ", entries)}");
        }

        [Fact]
        public async Task QueryCache_BypassedInsideCommand()
        {
            using var serviceProvider = BuildServices(
                options =>
                {
                    options.AddQuery<CachedCountQueryHandler>();
                    options.AddCommand<NestedCachedQueryCommandHandler>();
                },
                builder => builder.AddQueryCaching());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // Prime the cache from outside a command.
            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);

            // The same query inside a command must bypass the cache (possible uncommitted state).
            var result = await domain.SendAsync(new NestedCachedQueryCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, log.Entries.Count(entry => entry == "cached-query-exec"));

            // Outside again: still served from the cache primed before the command.
            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);
            Assert.Equal(2, log.Entries.Count(entry => entry == "cached-query-exec"));
        }

        [Fact]
        public async Task QueryCache_KeyCollision_TreatedAsMiss()
        {
            using var serviceProvider = BuildServices(
                options =>
                {
                    options.AddQuery<CachedCountQueryHandler>();
                    options.AddQuery<CollidingUsersQueryHandler>();
                },
                builder => builder.AddQueryCaching());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            // Caches Result<int> under "user-count".
            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);

            // Same key, different result shape: must be a miss, not a wrong-typed crash.
            var collidingResult = await domain.QueryAsync(new CollidingUsersQuery(), TestContext.Current.CancellationToken);

            Assert.True(collidingResult.IsSuccess);
            Assert.Empty(collidingResult.Data);
        }

        [Fact]
        public async Task Transactions_DuplicateRegistration_SingleBehavior()
        {
            using var serviceProvider = BuildServices(
                options => options.AddCommand<PublishingCommandHandler>(),
                builder => builder
                    .AddTransactions<FakeTransactionFactory>()
                    .AddTransactions<FakeTransactionFactory>()
                    .AddTransactions());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            // One TransactionBehavior despite three registrations: exactly one begin/commit pair.
            Assert.Equal(["tx-begin", "command", "tx-commit"], log.Entries);
        }

        [Fact]
        public async Task QueryCache_RespectsRegistrationOrder()
        {
            // Transactions, then a user behavior, then caching: the cache must NOT be hoisted
            // above the user behavior - registration order is the contract.
            using var serviceProvider = BuildServices(
                options => options.AddQuery<CachedCountQueryHandler>(),
                builder => builder
                    .AddTransactions<FakeTransactionFactory>()
                    .AddBehavior<FirstLoggingBehavior>()
                    .AddQueryCaching<RecordingQueryCache>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);

            var entries = log.Entries.ToList();
            var behaviorIndex = entries.IndexOf("first:before");
            var cacheIndex = entries.IndexOf("cache-get:user-count");
            Assert.True(behaviorIndex >= 0 && cacheIndex > behaviorIndex,
                $"Expected the user behavior to run before the cache, but the log is: {string.Join(", ", entries)}");
        }

        [Fact]
        public async Task QueryCache_NestedInvalidation_AfterOuterCommit()
        {
            using var serviceProvider = BuildServices(
                options =>
                {
                    options.AddCommand<InvalidateCountCommandHandler>();
                    options.AddCommand<NestingInvalidateCommandHandler>();
                },
                builder => builder
                    .AddTransactions<FakeTransactionFactory>()
                    .AddQueryCaching<RecordingQueryCache>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new NestingInvalidateCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var entries = log.Entries.ToList();
            var commitIndex = entries.IndexOf("tx-commit");
            var removeIndex = entries.IndexOf("cache-remove:user-count");
            Assert.True(commitIndex >= 0 && removeIndex > commitIndex,
                $"Expected the nested command's invalidation after the outer commit, but the log is: {string.Join(", ", entries)}");
        }

        [Fact]
        public async Task QueryCache_ServedInDeferredHandler_AfterCommand()
        {
            using var serviceProvider = BuildServices(
                options =>
                {
                    options.AddQuery<CachedCountQueryHandler>();
                    options.AddCommand<PublishingCommandHandler>();
                    options.AddEvent<QueryingDeferredHandler>();
                },
                builder => builder.AddQueryCaching());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // Prime the cache, then run a command whose deferred handler re-queries: the handler
            // runs after the command completed, so it must be served from the cache.
            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);
            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Contains("deferred-query:42", log.Entries);
            Assert.Equal(1, log.Entries.Count(entry => entry == "cached-query-exec"));
        }

        [Fact]
        public async Task QueryCache_DoesNotTagForeignAmbientActivity()
        {
            using var serviceProvider = BuildServices(
                options => options.AddQuery<CachedCountQueryHandler>(),
                builder => builder.AddQueryCaching<RecordingQueryCache>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            // Without a BrandUp.Domain listener the dispatch span is absent and Activity.Current
            // is the host's ambient activity (e.g. the HTTP request span) - it must stay untagged.
            var ambient = new System.Diagnostics.Activity("host.request").Start();
            try
            {
                await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);

                Assert.DoesNotContain(ambient.TagObjects, tag => tag.Key == "brandup.cache");
            }
            finally
            {
                ambient.Stop();
            }
        }

        [Fact]
        public async Task QueryCache_InvalidationFailure_DoesNotFailCommand()
        {
            // The command already succeeded and committed: a throwing RemoveAsync must be
            // logged, not surfaced to the caller.
            using var serviceProvider = BuildServices(
                options => options.AddCommand<InvalidateCountCommandHandler>(),
                builder => builder.AddQueryCaching<ThrowOnRemoveQueryCache>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new InvalidateCountCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task AddHandlersFrom_ScansAssembly()
        {
            using var serviceProvider = BuildServices(options => options.AddHandlersFrom(
                typeof(DomainBehaviorsTest).Assembly,
                // the test assembly deliberately contains duplicate-handler fixtures
                type => type != typeof(AnotherUserByPhoneQueryHandler) && type != typeof(AnotherCounterCommandHandler)));
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var commandResult = await domain.SendAsync(new ManualTxCommand(), TestContext.Current.CancellationToken);
            var queryResult = await domain.QueryAsync(new UserCountQuery(), TestContext.Current.CancellationToken);
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            await eventPublisher.PublishAsync(new UserLeft { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(commandResult.IsSuccess);
            Assert.Equal(5, queryResult.Data);
            Assert.Contains("route-left:+1", scope.ServiceProvider.GetRequiredService<EventLog>().Entries);
        }

        [Fact]
        public async Task Observability_DispatchActivityEmitted()
        {
            var activities = new List<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == DomainDiagnostics.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    lock (activities)
                        activities.Add(activity);
                }
            };
            ActivitySource.AddActivityListener(listener);

            using var serviceProvider = BuildServices(options => options.AddCommand<ManualTxCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            await domain.SendAsync(new ManualTxCommand(), TestContext.Current.CancellationToken);

            // The listener is process-global and other tests may run concurrently: assert that
            // this dispatch produced a matching activity instead of expecting exactly one.
            Activity dispatchActivity;
            lock (activities)
                dispatchActivity = activities.Find(activity => activity.OperationName == "domain.command"
                    && Equals(activity.GetTagItem("brandup.request"), typeof(ManualTxCommand).FullName));
            Assert.NotNull(dispatchActivity);
            Assert.Equal("command", dispatchActivity.GetTagItem("brandup.kind"));
            Assert.Equal("success", dispatchActivity.GetTagItem("brandup.status"));
        }

        [Fact]
        public async Task Behavior_InvokingNextTwice_FailsLoudly()
        {
            // The pipeline walker shares one continuation delegate per dispatch: a second next()
            // past the handler must throw instead of silently re-running the handler.
            using var serviceProvider = BuildServices(
                options => options.AddCommand<ManualTxCommandHandler>(),
                builder => builder.AddBehavior<DoubleNextBehavior>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => domain.SendAsync(new ManualTxCommand(), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task QueryCache_ConfiguredMemoryCacheOverload()
        {
            using var serviceProvider = BuildServices(
                options => options.AddQuery<CachedCountQueryHandler>(),
                builder => builder.AddQueryCaching(cache => cache.SizeLimit = 100));
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            Assert.IsType<MemoryQueryCache>(serviceProvider.GetRequiredService<IQueryCache>());

            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);
            await domain.QueryAsync(new CachedCountQuery(), TestContext.Current.CancellationToken);

            Assert.Single(log.Entries, "cached-query-exec");
        }

        [Fact]
        public async Task Validation_RunsIValidatableObject()
        {
            // The default validator gates on validation attributes OR IValidatableObject: a
            // command whose only validation lives in Validate() must still be checked.
            using var serviceProvider = BuildServices(options => options.AddCommand<SelfValidatingCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var invalid = await domain.SendAsync(new SelfValidatingCommand { Value = -1 }, TestContext.Current.CancellationToken);
            Assert.False(invalid.IsSuccess);
            Assert.Contains(invalid.Errors, error => error.Message == "Value must not be negative.");

            var valid = await domain.SendAsync(new SelfValidatingCommand { Value = 1 }, TestContext.Current.CancellationToken);
            Assert.True(valid.IsSuccess);
        }

        [Fact]
        public async Task Behavior_InvokingNextTwice_AfterShortCircuit_FailsLoudly()
        {
            // The dangerous variant: the second next() must not resume the walk past the
            // short-circuiting behavior and run the handler without its check.
            using var serviceProvider = BuildServices(
                options => options.AddCommand<PublishingCommandHandler>(),
                builder => builder
                    .AddBehavior<DoubleNextBehavior>()
                    .AddBehavior<ShortCircuitBehavior>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken));

            Assert.Empty(log.Entries); // the handler never ran
        }

        [Fact]
        public async Task Validation_SeesAttributeOnOverriddenBaseProperty()
        {
            // The validator's type gate must see [Required] declared on the base of an
            // overridden virtual property (plain reflection IsDefined misses it).
            using var serviceProvider = BuildServices(options => options.AddCommand<OverridingNamedCommandHandler>());
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var invalid = await domain.SendAsync(new OverridingNamedCommand(), TestContext.Current.CancellationToken);
            Assert.False(invalid.IsSuccess);

            var valid = await domain.SendAsync(new OverridingNamedCommand { Name = "ok" }, TestContext.Current.CancellationToken);
            Assert.True(valid.IsSuccess);
        }

        public sealed class DoubleNextBehavior : IDomainBehavior
        {
            public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
            {
                await next();
                return await next();
            }
        }

        public abstract class NamedCommandBase : ICommand
        {
            [System.ComponentModel.DataAnnotations.Required]
            public virtual string Name { get; set; }
        }

        public sealed class OverridingNamedCommand : NamedCommandBase
        {
            public override string Name { get; set; }
        }

        public sealed class OverridingNamedCommandHandler : ICommandHandler<OverridingNamedCommand>
        {
            public Task<Result> HandleAsync(OverridingNamedCommand command, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result.Success());
            }
        }

        public sealed class SelfValidatingCommand : ICommand, System.ComponentModel.DataAnnotations.IValidatableObject
        {
            public int Value { get; set; }

            public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(System.ComponentModel.DataAnnotations.ValidationContext validationContext)
            {
                if (Value < 0)
                    yield return new System.ComponentModel.DataAnnotations.ValidationResult("Value must not be negative.", [nameof(Value)]);
            }
        }

        public sealed class SelfValidatingCommandHandler : ICommandHandler<SelfValidatingCommand>
        {
            public Task<Result> HandleAsync(SelfValidatingCommand command, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result.Success());
            }
        }
    }
}
