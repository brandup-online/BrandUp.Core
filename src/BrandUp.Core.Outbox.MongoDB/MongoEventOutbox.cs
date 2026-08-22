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

            // Bulk claim in three round trips instead of one FindOneAndUpdate per event, so the
            // pass cost does not scale with BatchSize × network RTT: pick candidate ids, stamp
            // them atomically with this pass's claim token (the pending filter is re-checked in
            // the update, so ids stolen by a concurrent processor are simply not stamped), then
            // fetch the stamped documents.
            var now = DateTime.UtcNow;
            var pendingFilter = Builders<OutboxEventDocument>.Filter.And(
                Builders<OutboxEventDocument>.Filter.Eq(document => document.DeliveredAt, null),
                Builders<OutboxEventDocument>.Filter.Eq(document => document.DeadAt, null),
                Builders<OutboxEventDocument>.Filter.Lt(document => document.Attempts, options.MaxAttempts),
                Builders<OutboxEventDocument>.Filter.Or(
                    Builders<OutboxEventDocument>.Filter.Eq(document => document.LockedUntil, null),
                    Builders<OutboxEventDocument>.Filter.Lt(document => document.LockedUntil, now)));

            var candidateIds = await collection.Find(pendingFilter)
                .Limit(batchSize)
                .Project(document => document.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            if (candidateIds.Count == 0)
                return [];

            // Candidates stolen by a concurrent processor mid-race are simply not stamped: that
            // pass may come back short (or empty) while the winner drains without the poll
            // delay - a latency trade, never a lost event.
            var claimToken = ObjectId.GenerateNewId();
            await collection.UpdateManyAsync(
                Builders<OutboxEventDocument>.Filter.And(
                    Builders<OutboxEventDocument>.Filter.In(document => document.Id, candidateIds),
                    pendingFilter),
                Builders<OutboxEventDocument>.Update
                    .Set(document => document.LockedUntil, now + options.LeaseDuration)
                    .Set(document => document.ClaimToken, claimToken)
                    .Inc(document => document.Attempts, 1),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // The id list keeps the fetch on the _id index; the token alone would be a full
            // collection scan (ClaimToken is deliberately unindexed).
            return await collection.Find(
                    Builders<OutboxEventDocument>.Filter.And(
                        Builders<OutboxEventDocument>.Filter.In(document => document.Id, candidateIds),
                        Builders<OutboxEventDocument>.Filter.Eq(document => document.ClaimToken, claimToken)))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        const string DeliveredTtlIndexName = "delivered_ttl";

        /// <summary>
        /// Creates the compound index backing the claim and sweep filters, plus a TTL index that
        /// expires delivered events after <see cref="MongoOutboxOptions.DeliveredRetention"/> (the
        /// collection would otherwise grow forever). Called once by
        /// <see cref="MongoOutboxProcessor"/> at startup; safe to call repeatedly.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
        {
            var keys = Builders<OutboxEventDocument>.IndexKeys
                .Ascending(document => document.DeliveredAt)
                .Ascending(document => document.DeadAt)
                .Ascending(document => document.Attempts)
                .Ascending(document => document.LockedUntil);

            await collection.Indexes.CreateOneAsync(new CreateIndexModel<OutboxEventDocument>(keys), cancellationToken: cancellationToken).ConfigureAwait(false);

            // TTL applies to DeliveredAt only: undelivered and dead documents carry no date there,
            // so MongoDB's TTL monitor never touches them.
            if (options.DeliveredRetention is not { } retention)
                return;

            var ttlModel = new CreateIndexModel<OutboxEventDocument>(
                Builders<OutboxEventDocument>.IndexKeys.Ascending(document => document.DeliveredAt),
                new CreateIndexOptions { Name = DeliveredTtlIndexName, ExpireAfter = retention });

            try
            {
                await collection.Indexes.CreateOneAsync(ttlModel, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (MongoCommandException exception) when (exception.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
            {
                // The retention changed since the index was created - recreate with the new expiry.
                await collection.Indexes.DropOneAsync(DeliveredTtlIndexName, cancellationToken).ConfigureAwait(false);
                await collection.Indexes.CreateOneAsync(ttlModel, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
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
