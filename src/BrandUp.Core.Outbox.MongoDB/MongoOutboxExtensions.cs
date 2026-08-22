using BrandUp.Events;
using BrandUp.Events.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BrandUp
{
    /// <summary>
    /// Registration extensions for the MongoDB event outbox.
    /// </summary>
    public static class MongoOutboxExtensions
    {
        /// <summary>
        /// Routes deferred domain events through a MongoDB outbox and starts the background
        /// delivery processor: <see cref="MongoEventOutbox"/> persists events at publish time
        /// (inside the command's transaction when
        /// <see cref="MongoOutboxOptions.SessionAccessor"/> is configured — pair with
        /// <c>AddTransactions</c>), <see cref="MongoOutboxProcessor"/> delivers them after commit
        /// with lease-based retries. Requires an <c>IMongoDatabase</c> in the container unless
        /// <see cref="MongoOutboxOptions.DatabaseAccessor"/> is set.
        /// </summary>
        /// <param name="builder">Domain builder.</param>
        /// <param name="configure">Optional outbox settings.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static IDomainBuilder AddMongoEventOutbox(this IDomainBuilder builder, Action<MongoOutboxOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (configure != null)
                builder.Services.Configure(configure);

            builder.Services.TryAddSingleton<IOutboxEventSerializer, JsonOutboxEventSerializer>();

            // One store instance per scope behind both faces: the concrete type carries the
            // delivery-side API, IEventOutbox the enqueue side.
            builder.Services.TryAddScoped<MongoEventOutbox>();
            builder.Services.TryAddScoped<IEventOutbox>(provider => provider.GetRequiredService<MongoEventOutbox>());
            builder.Services.AddHostedService<MongoOutboxProcessor>();

            return builder.UseEventOutbox();
        }
    }
}
