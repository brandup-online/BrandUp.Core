using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BrandUp.Events.MongoDB
{
    /// <summary>
    /// MongoDB <see cref="IEventOutbox"/>: enqueues deferred domain events into the outbox
    /// collection — inside the command's transaction when
    /// <see cref="MongoOutboxOptions.SessionAccessor"/> resolves the ambient session — and gives
    /// the delivery side (<see cref="MongoOutboxProcessor"/> or a custom processor) the
    /// claim/acknowledge API. Registered scoped via <c>AddMongoEventOutbox</c>.
    /// </summary>
    public sealed class MongoEventOutbox : IEventOutbox
    {
        readonly IServiceProvider serviceProvider;
        readonly MongoOutboxOptions options;
        readonly IOutboxEventSerializer serializer;
        readonly IMongoCollection<OutboxEventDocument> collection;

        /// <summary>
        /// Creates the outbox over the database resolved by
        /// <see cref="MongoOutboxOptions.DatabaseAccessor"/> (an <see cref="IMongoDatabase"/> from
        /// the container by default).
        /// </summary>
        /// <param name="serviceProvider">Service provider of the executing scope.</param>
        /// <param name="options">Outbox settings.</param>
        /// <param name="serializer">Event serializer.</param>
        public MongoEventOutbox(IServiceProvider serviceProvider, IOptions<MongoOutboxOptions> options, IOutboxEventSerializer serializer)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            var database = this.options.DatabaseAccessor?.Invoke(serviceProvider)
                ?? serviceProvider.GetRequiredService<IMongoDatabase>();
            collection = database.GetCollection<OutboxEventDocument>(this.options.CollectionName);
        }

        /// <inheritdoc/>
        public async Task EnqueueAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            var document = new OutboxEventDocument
            {
                Id = ObjectId.GenerateNewId(),
                EventType = serializer.GetTypeName(@event),
                Payload = serializer.Serialize(@event),
                CreatedAt = DateTime.UtcNow
            };

            // On the ambient session the insert joins the command's transaction: events of a
            // rolled-back command roll back with it - the guarantee the outbox exists for.
            var session = options.SessionAccessor?.Invoke(serviceProvider);
            if (session != null)
                await collection.InsertOneAsync(session, document, cancellationToken: cancellationToken).ConfigureAwait(false);
            else
                await collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Atomically claims up to <paramref name="batchSize"/> pending events for delivery: not
        /// delivered, not dead, attempts not exhausted, lease absent or expired. Claiming renews
        /// the lease (<see cref="MongoOutboxOptions.LeaseDuration"/>) and counts an attempt.
        /// </summary>
        /// <param name="batchSize">Maximum events to claim.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public async Task<IReadOnlyList<OutboxEventDocument>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

            // Events that burned their attempts without an explicit failure mark (e.g. a pass
            // aborted before reaching them) must surface as dead - otherwise they silently drop
            // out of the claim filter, undelivered and invisible to dead-letter monitoring. The
            // lease condition keeps another processor's in-progress final attempt out of the
            // sweep: an exhausted event is abandoned only once its lease has lapsed.
            var sweepNow = DateTime.UtcNow;
            await collection.UpdateManyAsync(
                Builders<OutboxEventDocument>.Filter.And(
                    Builders<OutboxEventDocument>.Filter.Eq(document => document.DeliveredAt, null),
                    Builders<OutboxEventDocument>.Filter.Eq(document => document.DeadAt, null),
                    Builders<OutboxEventDocument>.Filter.Gte(document => document.Attempts, options.MaxAttempts),
                    Builders<OutboxEventDocument>.Filter.Or(
                        Builders<OutboxEventDocument>.Filter.Eq(document => document.LockedUntil, null),
                        Builders<OutboxEventDocument>.Filter.Lt(document => document.LockedUntil, sweepNow))),
                Builders<OutboxEventDocument>.Update.Set(document => document.DeadAt, sweepNow),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var claimed = new List<OutboxEventDocument>();
            var updateOptions = new FindOneAndUpdateOptions<OutboxEventDocument> { ReturnDocument = ReturnDocument.After };

            while (claimed.Count < batchSize)
            {
                var now = DateTime.UtcNow;
                var filter = Builders<OutboxEventDocument>.Filter.And(
                    Builders<OutboxEventDocument>.Filter.Eq(document => document.DeliveredAt, null),
                    Builders<OutboxEventDocument>.Filter.Eq(document => document.DeadAt, null),
                    Builders<OutboxEventDocument>.Filter.Lt(document => document.Attempts, options.MaxAttempts),
                    Builders<OutboxEventDocument>.Filter.Or(
                        Builders<OutboxEventDocument>.Filter.Eq(document => document.LockedUntil, null),
                        Builders<OutboxEventDocument>.Filter.Lt(document => document.LockedUntil, now)));
                var update = Builders<OutboxEventDocument>.Update
                    .Set(document => document.LockedUntil, now + options.LeaseDuration)
                    .Inc(document => document.Attempts, 1);

                var document = await collection.FindOneAndUpdateAsync(filter, update, updateOptions, cancellationToken).ConfigureAwait(false);
                if (document == null)
                    break;

                claimed.Add(document);
            }

            return claimed;
        }

        /// <summary>
        /// Creates the compound index backing the claim and sweep filters. Called once by
        /// <see cref="MongoOutboxProcessor"/> at startup; safe to call repeatedly.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
        {
            var keys = Builders<OutboxEventDocument>.IndexKeys
                .Ascending(document => document.DeliveredAt)
                .Ascending(document => document.DeadAt)
                .Ascending(document => document.Attempts)
                .Ascending(document => document.LockedUntil);

            return collection.Indexes.CreateOneAsync(new CreateIndexModel<OutboxEventDocument>(keys), cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Marks a claimed event as delivered; it is never retried again.
        /// </summary>
        /// <param name="id">Identifier of the claimed event.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public Task MarkDeliveredAsync(ObjectId id, CancellationToken cancellationToken = default)
        {
            var update = Builders<OutboxEventDocument>.Update
                .Set(document => document.DeliveredAt, DateTime.UtcNow)
                .Set(document => document.LockedUntil, null);

            return collection.UpdateOneAsync(Builders<OutboxEventDocument>.Filter.Eq(document => document.Id, id), update, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Records a failed delivery attempt of a claimed event. The lease keeps it invisible
        /// until expiry (the retry backoff); an event whose attempts are exhausted is marked dead.
        /// </summary>
        /// <param name="id">Identifier of the claimed event.</param>
        /// <param name="attempts">Attempt count observed at claim time.</param>
        /// <param name="error">Failure message to record.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public Task MarkFailedAsync(ObjectId id, int attempts, string error, CancellationToken cancellationToken = default)
        {
            var update = Builders<OutboxEventDocument>.Update.Set(document => document.LastError, error);
            if (attempts >= options.MaxAttempts)
                update = update.Set(document => document.DeadAt, DateTime.UtcNow);

            return collection.UpdateOneAsync(Builders<OutboxEventDocument>.Filter.Eq(document => document.Id, id), update, cancellationToken: cancellationToken);
        }
    }
}
