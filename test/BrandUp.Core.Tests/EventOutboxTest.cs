using System;
using System.Threading.Tasks;
using BrandUp.Events;
using BrandUp.Example.Behaviors;
using BrandUp.Example.Commands;
using BrandUp.Example.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class EventOutboxTest
    {
        static ServiceProvider BuildServices(Action<DomainOptions> configure, bool optIn = true)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<EventLog>();
            serviceCollection.AddSingleton<DisposeProbe>();
            serviceCollection.AddSingleton<FakeEventOutbox>();
            serviceCollection.AddSingleton<IEventOutbox>(provider => provider.GetRequiredService<FakeEventOutbox>());

            var builder = serviceCollection.AddDomain(configure);
            if (optIn)
                builder.UseEventOutbox();

            return serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public async Task Publish_OutboxRegisteredWithoutOptIn_DeferredRunsInProcess()
        {
            // Rerouting is an explicit opt-in: a bare IEventOutbox registration changes nothing.
            using var serviceProvider = BuildServices(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            }, optIn: false);
            using var scope = serviceProvider.CreateAsyncScope();
            var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();
            var outbox = scope.ServiceProvider.GetRequiredService<FakeEventOutbox>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(["command", "deferred:+1"], log.Entries);
            Assert.Empty(outbox.Events);
        }

        [Fact]
        public async Task Publish_DeferredGoesToOutbox_ImmediateRunsInProcess()
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
            var outbox = scope.ServiceProvider.GetRequiredService<FakeEventOutbox>();

            var result = await domain.SendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            // Immediate handler ran in process; the deferred one went to durable storage instead.
            Assert.Equal(["first:+1", "command"], log.Entries);
            var storedEvent = Assert.Single(outbox.Events);
            Assert.Equal("+1", Assert.IsType<UserJoined>(storedEvent).Phone);
        }

        [Fact]
        public async Task DispatchDeferred_RunsDeferredHandlersOnly()
        {
            using var serviceProvider = BuildServices(options =>
            {
                options.AddEvent<UserJoinedFirstHandler>();
                options.AddEvent<UserJoinedDeferredHandler>();
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var log = scope.ServiceProvider.GetRequiredService<EventLog>();

            // What an outbox processor does with an event read back from storage.
            await dispatcher.DispatchDeferredAsync(new UserJoined { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.Equal(["deferred:+1"], log.Entries);
        }
    }
}
