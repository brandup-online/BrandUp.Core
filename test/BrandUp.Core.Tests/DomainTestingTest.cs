using System;
using System.Threading.Tasks;
using BrandUp.Example.Commands;
using BrandUp.Example.Events;
using BrandUp.Example.Items;
using BrandUp.Testing;
using BrandUp.Testing.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace BrandUp
{
    public class DomainTestingTest
    {
        static DomainTestHost CreateHost(Action<DomainOptions> configureDomain, Action<Builder.IDomainBuilder> configureBuilder = null)
        {
            return DomainTestHost.Create(
                configureDomain,
                configureBuilder,
                services =>
                {
                    services.AddSingleton<EventLog>();
                    services.AddSingleton<DisposeProbe>();
                    services.AddSingleton<ContextProbe>();
                });
        }

        [Fact]
        public async Task TestHost_DispatchAndAssert()
        {
            await using var host = CreateHost(options => options.AddCommand<PublishingCommandHandler>());

            await host.Domain.AssertSendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task AssertSendErrorAsync_MatchesCode()
        {
            await using var host = CreateHost(options => options.AddCommand<PublishingCommandHandler>());

            var error = await host.Domain.AssertSendErrorAsync(new PublishingCommand { Phone = "+1", Fail = true }, code: "fail", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("fail", error.Code);
        }

        [Fact]
        public void AssertSuccess_OnFailedResult_Throws()
        {
            var exception = Assert.ThrowsAny<DomainAssertException>(
                () => Result.Error("code", "Something failed.", ErrorKind.Conflict).AssertSuccess());

            // The message lists the errors with codes and kinds.
            Assert.Contains("[code/Conflict] Something failed.", exception.Message);
        }

        [Fact]
        public void AssertError_MatchesCodeAndKind()
        {
            var result = Result.Error("user-not-found", "User not found.", ErrorKind.NotFound);

            var error = result.AssertError("user-not-found", ErrorKind.NotFound);
            Assert.Equal(ErrorKind.NotFound, error.Kind);

            Assert.ThrowsAny<DomainAssertException>(() => result.AssertError("other-code"));
            Assert.ThrowsAny<DomainAssertException>(() => Result.Success().AssertError());
        }

        [Fact]
        public void XunitAdapter_ReportsAssertionFailure()
        {
            XunitDomainAssert.Use();

            var exception = Assert.Throws<XunitDomainAssertException>(
                () => Result.Error("code", "message").AssertSuccess());

            // xUnit treats it as a native assertion failure.
            Assert.IsAssignableFrom<IAssertionException>(exception);
        }

        [Fact]
        public async Task EventCapture_CollectsPublishedEvents()
        {
            await using var host = CreateHost(options =>
            {
                options.AddCommand<PublishingCommandHandler>();
                options.CaptureEvent<UserJoined>();
            });

            await host.Domain.AssertSendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            Assert.Equal("+1", host.Events.Single<UserJoined>().Phone);
            host.Events.AssertPublished<UserJoined>(e => e.Phone == "+1");
            host.Events.AssertNotPublished<UserLeft>();
        }

        [Fact]
        public async Task TestTransactions_RecordLifecycle()
        {
            await using var host = CreateHost(
                options => options.AddCommand<PublishingCommandHandler>(),
                builder => builder.AddTestTransactions());

            await host.Domain.AssertSendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);
            await host.Domain.AssertSendErrorAsync(new PublishingCommand { Phone = "+2", Fail = true }, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["begin", "commit", "begin", "abort"], host.Transactions.Operations);
        }

        [Fact]
        public async Task TestOutbox_CollectsAndDelivers()
        {
            await using var host = CreateHost(
                options =>
                {
                    options.AddCommand<PublishingCommandHandler>();
                    options.AddEvent<UserJoinedDeferredHandler>();
                },
                builder => builder.AddTestEventOutbox());
            var log = host.GetRequiredService<EventLog>();

            await host.Domain.AssertSendAsync(new PublishingCommand { Phone = "+1" }, TestContext.Current.CancellationToken);

            // Deferred handler did not run in process; the event is waiting in the outbox.
            Assert.Equal(["command"], log.Entries);
            Assert.Single(host.Outbox.Enqueued);

            var delivered = await host.Outbox.DeliverAsync(host.EventDispatcher, TestContext.Current.CancellationToken);

            Assert.Equal(1, delivered);
            Assert.Empty(host.Outbox.Enqueued);
            Assert.Equal(["command", "deferred:+1"], log.Entries);
        }

        [Fact]
        public async Task AssertItemAsync_FindsAndChecks()
        {
            await using var host = CreateHost(
                options => { },
                builder => builder.AddItemProvider<UserProvider>());

            var user = await host.Domain.AssertItemAsync<Guid, User>(Guid.Empty,
                item => Assert.NotNull(item.Phone),
                TestContext.Current.CancellationToken);

            Assert.NotNull(user);
        }
    }
}
