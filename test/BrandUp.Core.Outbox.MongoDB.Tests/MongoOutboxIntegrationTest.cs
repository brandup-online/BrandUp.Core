using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Events;
using BrandUp.Events.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace BrandUp
{
    [Collection("mongo")]
    public class MongoOutboxIntegrationTest(MongoRunnerFixture fixture)
    {
        const string CollectionName = "brandup.outbox";

        static ServiceProvider BuildServices(IMongoDatabase database, Action<MongoOutboxOptions> configureOutbox = null, Action<IServiceCollection> configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(database);
            services.AddSingleton<ReceivedEvents>();
            configureServices?.Invoke(services);

            services.AddDomain(options => options.AddEvent<TestDeferredHandler>())
                .AddMongoEventOutbox(configureOutbox);

            return services.BuildServiceProvider();
        }

        IMongoCollection<OutboxEventDocument> Documents(IMongoDatabase database)
        {
            return database.GetCollection<OutboxEventDocument>(CollectionName);
        }

        [Fact]
        public async Task Enqueue_PersistsDocumentWithShortElementNames()
        {
            var database = fixture.GetDatabaseOrSkip();
            using var serviceProvider = BuildServices(database);
            using var scope = serviceProvider.CreateAsyncScope();

            await scope.ServiceProvider.GetRequiredService<IEventOutbox>()
                .EnqueueAsync(new TestEvent { Value = "first" }, TestContext.Current.CancellationToken);

            var raw = await database.GetCollection<BsonDocument>(CollectionName)
                .Find(FilterDefinition<BsonDocument>.Empty).SingleAsync(TestContext.Current.CancellationToken);
            Assert.True(raw.Contains("t"));
            Assert.True(raw.Contains("p"));
            Assert.True(raw.Contains("ts"));
            Assert.Equal(BsonType.DateTime, raw["ts"].BsonType);

            var document = await Documents(database).Find(_ => true).SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, document.Attempts);
            Assert.Null(document.DeliveredAt);
        }

        [Fact]
        public async Task ClaimProtocol_LeasesAndAcknowledges()
        {
            var database = fixture.GetDatabaseOrSkip();
            using var serviceProvider = BuildServices(database);
            using var scope = serviceProvider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<MongoEventOutbox>();

            await outbox.EnqueueAsync(new TestEvent { Value = "a" }, TestContext.Current.CancellationToken);
            await outbox.EnqueueAsync(new TestEvent { Value = "b" }, TestContext.Current.CancellationToken);

            var claimed = await outbox.ClaimPendingAsync(10, TestContext.Current.CancellationToken);
            Assert.Equal(2, claimed.Count);
            Assert.All(claimed, document => Assert.Equal(1, document.Attempts));

            // Leased events are invisible to a second claim.
            Assert.Empty(await outbox.ClaimPendingAsync(10, TestContext.Current.CancellationToken));

            await outbox.MarkDeliveredAsync(claimed[0].Id, TestContext.Current.CancellationToken);
            await outbox.MarkFailedAsync(claimed[1].Id, claimed[1].Attempts, "boom", TestContext.Current.CancellationToken);

            var documents = await Documents(database).Find(_ => true).ToListAsync(TestContext.Current.CancellationToken);
            var delivered = documents.Single(document => document.Id == claimed[0].Id);
            Assert.NotNull(delivered.DeliveredAt);
            var failed = documents.Single(document => document.Id == claimed[1].Id);
            Assert.Equal("boom", failed.LastError);
            Assert.Null(failed.DeadAt); // attempts not exhausted (default MaxAttempts = 5)
        }

        [Fact]
        public async Task FailedDelivery_RetriesAfterLease_ThenDead()
        {
            var database = fixture.GetDatabaseOrSkip();
            using var serviceProvider = BuildServices(database, outbox =>
            {
                // The lease must comfortably outlast the Mongo round-trips between the claim and
                // the "still leased" assertion, or a slow CI turns this into a flake.
                outbox.LeaseDuration = TimeSpan.FromSeconds(2);
                outbox.MaxAttempts = 2;
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<MongoEventOutbox>();

            await outbox.EnqueueAsync(new TestEvent { Value = "retry" }, TestContext.Current.CancellationToken);

            var first = Assert.Single(await outbox.ClaimPendingAsync(1, TestContext.Current.CancellationToken));
            await outbox.MarkFailedAsync(first.Id, first.Attempts, "fail-1", TestContext.Current.CancellationToken);

            // Still leased: the lease is the retry backoff.
            Assert.Empty(await outbox.ClaimPendingAsync(1, TestContext.Current.CancellationToken));

            await Task.Delay(3000, TestContext.Current.CancellationToken);
            var second = Assert.Single(await outbox.ClaimPendingAsync(1, TestContext.Current.CancellationToken));
            Assert.Equal(2, second.Attempts);
            await outbox.MarkFailedAsync(second.Id, second.Attempts, "fail-2", TestContext.Current.CancellationToken);

            await Task.Delay(3000, TestContext.Current.CancellationToken);
            Assert.Empty(await outbox.ClaimPendingAsync(1, TestContext.Current.CancellationToken));

            var document = await Documents(database).Find(_ => true).SingleAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(document.DeadAt);
            Assert.Equal("fail-2", document.LastError);
        }

        [Fact]
        public async Task ExhaustedWithoutFailureMark_IsSweptDead()
        {
            var database = fixture.GetDatabaseOrSkip();
            using var serviceProvider = BuildServices(database, outbox =>
            {
                outbox.LeaseDuration = TimeSpan.FromSeconds(1);
                outbox.MaxAttempts = 1;
            });
            using var scope = serviceProvider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<MongoEventOutbox>();

            await outbox.EnqueueAsync(new TestEvent { Value = "zombie" }, TestContext.Current.CancellationToken);

            // Claimed (attempts exhausted) but never acknowledged - a pass aborted mid-batch.
            Assert.Single(await outbox.ClaimPendingAsync(1, TestContext.Current.CancellationToken));

            await Task.Delay(2000, TestContext.Current.CancellationToken);
            Assert.Empty(await outbox.ClaimPendingAsync(1, TestContext.Current.CancellationToken));

            // The claim sweep surfaced it as dead instead of leaving it invisible forever.
            var document = await Documents(database).Find(_ => true).SingleAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(document.DeadAt);
        }

        [Fact]
        public async Task Enqueue_JoinsAmbientTransaction()
        {
            var client = fixture.ClientOrSkip();
            var database = fixture.GetDatabaseOrSkip();
            await database.CreateCollectionAsync(CollectionName, cancellationToken: TestContext.Current.CancellationToken);

            using var serviceProvider = BuildServices(database,
                outbox => outbox.SessionAccessor = provider => provider.GetService<SessionHolder>()?.Session,
                services => services.AddScoped<SessionHolder>());
            using var scope = serviceProvider.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IEventOutbox>();
            var holder = scope.ServiceProvider.GetRequiredService<SessionHolder>();

            using (var session = await client.StartSessionAsync(cancellationToken: TestContext.Current.CancellationToken))
            {
                holder.Session = session;
                session.StartTransaction();
                await outbox.EnqueueAsync(new TestEvent { Value = "rolled-back" }, TestContext.Current.CancellationToken);
                await session.AbortTransactionAsync(TestContext.Current.CancellationToken);
            }

            // The event of a rolled-back command rolled back with it.
            Assert.Equal(0, await Documents(database).CountDocumentsAsync(_ => true, cancellationToken: TestContext.Current.CancellationToken));

            using (var session = await client.StartSessionAsync(cancellationToken: TestContext.Current.CancellationToken))
            {
                holder.Session = session;
                session.StartTransaction();
                await outbox.EnqueueAsync(new TestEvent { Value = "committed" }, TestContext.Current.CancellationToken);
                await session.CommitTransactionAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(1, await Documents(database).CountDocumentsAsync(_ => true, cancellationToken: TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Processor_DeliversHealthyEventsDespitePoison()
        {
            var database = fixture.GetDatabaseOrSkip();
            using var serviceProvider = BuildServices(database, outbox =>
            {
                outbox.PollInterval = TimeSpan.FromMilliseconds(100);
                outbox.LeaseDuration = TimeSpan.FromMilliseconds(500);
            });

            // A poison document: resolvable event type, malformed payload. It must be marked
            // failed without starving the healthy event claimed in the same pass.
            var serializer = serviceProvider.GetRequiredService<IOutboxEventSerializer>();
            await Documents(database).InsertOneAsync(new OutboxEventDocument
            {
                Id = ObjectId.GenerateNewId(),
                EventType = serializer.GetTypeName(new TestEvent { Value = "poison" }),
                Payload = "{ not json",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken: TestContext.Current.CancellationToken);

            using (var scope = serviceProvider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IEventOutbox>()
                    .EnqueueAsync(new TestEvent { Value = "healthy" }, TestContext.Current.CancellationToken);
            }

            var processor = Assert.IsType<MongoOutboxProcessor>(Assert.Single(serviceProvider.GetServices<IHostedService>()));
            await processor.StartAsync(TestContext.Current.CancellationToken);
            try
            {
                var received = serviceProvider.GetRequiredService<ReceivedEvents>();
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (!received.Values.Contains("healthy") && DateTime.UtcNow < deadline)
                    await Task.Delay(100, TestContext.Current.CancellationToken);

                Assert.Contains("healthy", received.Values);

                var poison = await Documents(database)
                    .Find(document => document.LastError != null)
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
                Assert.NotNull(poison);
                Assert.Null(poison.DeliveredAt);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        public sealed class TestEvent : IDomainEvent
        {
            public string Value { get; init; }
        }

        public sealed class ReceivedEvents
        {
            readonly List<string> values = [];

            public IReadOnlyList<string> Values
            {
                get
                {
                    lock (values)
                        return [.. values];
                }
            }

            public void Add(string value)
            {
                lock (values)
                    values.Add(value);
            }
        }

        public sealed class TestDeferredHandler(ReceivedEvents received) : IDeferredDomainEventHandler<TestEvent>
        {
            public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken = default)
            {
                received.Add(@event.Value);
                return Task.CompletedTask;
            }
        }

        public sealed class SessionHolder
        {
            public IClientSessionHandle Session { get; set; }
        }
    }
}
