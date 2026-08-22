using MongoDB.Driver;

namespace BrandUp.Events.MongoDB
{
    /// <summary>
    /// Settings of the MongoDB event outbox (see <see cref="MongoEventOutbox"/> and
    /// <see cref="MongoOutboxProcessor"/>).
    /// </summary>
    public sealed class MongoOutboxOptions
    {
        /// <summary>
        /// Name of the collection holding outbox documents.
        /// </summary>
        public string CollectionName { get; set; } = "brandup.outbox";

        /// <summary>
        /// How often the background processor polls for pending events.
        /// </summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How long a claimed event stays invisible to other processors. Acts as the retry
        /// backoff: a failed delivery is retried after the lease expires.
        /// </summary>
        public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Delivery attempts before the event is marked dead (poison) and stops being retried.
        /// </summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// Maximum events claimed per polling pass.
        /// </summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// Resolves the database holding the outbox collection; by default
        /// <see cref="IMongoDatabase"/> is taken from the service provider.
        /// </summary>
        public Func<IServiceProvider, IMongoDatabase>? DatabaseAccessor { get; set; }

        /// <summary>
        /// Resolves the ambient session of the executing scope, so enqueueing joins the command's
        /// transaction — the whole point of the outbox. With BrandUp.MongoDB:
        /// <c>options.SessionAccessor = provider =&gt; provider.GetService&lt;MongoDbSession&gt;()?.Current</c>.
        /// <see langword="null"/> (or a <see langword="null"/> result) enqueues without a session —
        /// events of a command that later fails would be delivered anyway.
        /// </summary>
        public Func<IServiceProvider, IClientSessionHandle?>? SessionAccessor { get; set; }
    }
}
