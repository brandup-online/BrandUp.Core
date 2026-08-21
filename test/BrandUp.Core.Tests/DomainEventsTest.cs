using System;
using System.Threading.Tasks;
using BrandUp.Events;
using BrandUp.Example.Commands;
using BrandUp.Example.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class DomainEventsTest
    {
        static ServiceProvider BuildServices(Action<DomainOptions> configure)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<EventLog>();
            serviceCollection.AddSingleton<DisposeProbe>();
            serviceCollection.AddSingleton<ContextProbe>();
            serviceCollection.AddDomain(configure);

            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task SendAsync_MultiConstructorHandler_RegistersAndDispatches()
        {
            // Registration must not throw for a handler with several public constructors;
            // dispatch picks the DI-satisfiable one.
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<MultiCtorCommandHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();

            var result = await domain.SendAsync(new MultiCtorCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task PublishAsync_IntoEndedCommandScope_RunsImmediately()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<CaptureContextCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            var probe = scope.ServiceProvider.GetRequiredService<ContextProbe>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new CaptureContextCommand(), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess);

            // Simulate a fire-and-forget task that inherited the command's ExecutionContext and
            // publishes after the command ended: the event must run immediately, not vanish.
            Task publishTask = Task.CompletedTask;
            System.Threading.ExecutionContext.Run(probe.Context,
                _ => publishTask = eventPublisher.PublishAsync(new UserJoined { Phone = "+late" }, TestContext.Current.CancellationToken),
                null);
            await publishTask;

            Assert.Equal(["deferred:+late"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_HandlerDisposeThrows_DeferredDiscarded()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<ThrowOnDisposeCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // The caller observes an exception, so the deferred handlers must not have run.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => domain.SendAsync(new ThrowOnDisposeCommand(), TestContext.Current.CancellationToken));

            Assert.Equal(["command"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_PublishThroughForeignScope_FormsOwnBoundary()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<CrossScopeCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // A publisher from a different DI scope must not defer into this command's scope:
            // its handlers run immediately, constructed from its own service provider.
            var result = await domain.SendAsync(new CrossScopeCommand(), TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["deferred:+inner", "command"], log.Entries);
        }

        [Fact]
        public async Task PublishAsync_HandlersRunInRegistrationOrder()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddEvent<UserJoinedFirstHandler>();
                options.AddEvent<UserJoinedSecondHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await eventPublisher.PublishAsync(new UserJoined { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.Equal(["first:+1", "second:+1"], log.Entries);
        }

        [Fact]
        public async Task PublishAsync_NoHandlers_NoOp()
        {
            using var serviceProvider = BuildServices(options => { });
            using var scope = serviceProvider.CreateAsyncScope();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

            await eventPublisher.PublishAsync(new UserJoined { Phone = "+1" }, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PublishAsync_NoHandlers_Throws_WhenRequired()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.RequireEventHandlers = true;
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => eventPublisher.PublishAsync(new UserJoined { Phone = "+1" }, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task PublishAsync_DeferredHandler_OutsideCommand_RunsImmediately()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await eventPublisher.PublishAsync(new UserJoined { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.Equal(["deferred:+1"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_ImmediateHandler_RunsAtPublish_DeferredAfterCommand()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddEvent<UserJoinedFirstHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["first:+1", "command", "deferred:+1"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_DeferredHandler_DiscardedOnErrorResult()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1", Fail = true }, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(["command"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_DeferredHandler_DiscardedOnException()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => domain.SendAsync(new PublishingCommand { Phone = "+1", Throw = true }, TestContext.Current.CancellationToken));

            Assert.Equal(["command"], log.Entries);

            // A later successful command must not resurrect the discarded handlers.
            await domain.SendAsync(new PublishingCommand { Phone = "+2" }, TestContext.Current.CancellationToken);
            Assert.Equal(["command", "command", "deferred:+2"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_NestedCommand_DeferredRunsAfterOutermost()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddCommand<NestingCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            var result = await domain.SendAsync(new NestingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["command", "outer-command", "deferred:+1"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_NestedCommandFails_InnerDeferredDiscarded()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddCommand<NestingCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // The inner command fails; the outer compensates and succeeds. The inner command's
            // deferred handler must not run: its fact did not survive.
            var result = await domain.SendAsync(new NestingCommand { Phone = "+1", InnerFail = true, SwallowInnerError = true }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["command", "outer-command"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_DeferredHandlerThrows_ResultKept_SiblingsRun()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddEvent<ThrowingDeferredHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // A deferred handler failure is logged, does not fail the completed command and does
            // not stop the remaining deferred handlers.
            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["command", "deferred:+1"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_DeferredHandler_RunsWithNonCancellableToken()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddEvent<TokenProbeDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // The command runs with a cancellable token; the deferred handler must not: by flush
            // time the command has succeeded and its caller may already be gone.
            using var cts = new System.Threading.CancellationTokenSource();
            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, cts.Token);

            Assert.True(result.IsSuccess);
            Assert.Equal(["command", "token-cancellable:False"], log.Entries);
        }

        [Fact]
        public async Task SendAsync_ParallelCommands_DeferredIsolated()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await Task.WhenAll(
                domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken),
                domain.SendAsync(new PublishingCommand { Phone = "+2" }, TestContext.Current.CancellationToken));

            // Each parallel command flushes exactly its own deferred handler.
            Assert.Equal(4, log.Entries.Count);
            Assert.Single(log.Entries, "deferred:+1");
            Assert.Single(log.Entries, "deferred:+2");
        }

        [Fact]
        public async Task PublishAsync_MultiEventHandler_RoutesByEventType()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddEvent<UserEventsRouteHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            await eventPublisher.PublishAsync(new UserJoined { Phone = "+1" }, TestContext.Current.CancellationToken);
            await eventPublisher.PublishAsync(new UserLeft { Phone = "+2" }, TestContext.Current.CancellationToken);

            Assert.Equal(["route-joined:+1", "route-left:+2"], log.Entries);
        }

        [Fact]
        public async Task PublishAsync_HandlerDisposedAfterInvoke()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddEvent<DisposableEventHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
            var probe = scope.ServiceProvider.GetRequiredService<DisposeProbe>();

            await eventPublisher.PublishAsync(new UserJoined { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(probe.Disposed);
        }

        [Fact]
        public void AddEvent_Invalid()
        {
            var options = new DomainOptions();

            Assert.Throws<InvalidOperationException>(() =>
            {
                options.AddEvent<string>();
            });
        }

        [Fact]
        public void AddEvent_DuplicateHandler_Throws()
        {
            var options = new DomainOptions();
            options.AddEvent<UserJoinedFirstHandler>();

            Assert.Throws<InvalidOperationException>(() =>
            {
                options.AddEvent<UserJoinedFirstHandler>();
            });
        }
    }
}
