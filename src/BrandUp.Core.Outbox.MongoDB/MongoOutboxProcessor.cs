using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrandUp.Events.MongoDB
{
    /// <summary>
    /// Background delivery side of the MongoDB outbox: polls for pending events, dispatches each
    /// through <see cref="IDomainEventDispatcher"/> in its own scope, acknowledges successes and
    /// records failures for lease-based retry. A malformed or unresolvable event is marked failed
    /// without disturbing the rest of the batch. Delivery is at-least-once — deferred handlers
    /// must be idempotent. Registered by <c>AddMongoEventOutbox</c>.
    /// </summary>
    public sealed class MongoOutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<MongoOutboxOptions> options, ILogger<MongoOutboxProcessor>? logger = null) : BackgroundService
    {
        readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        readonly MongoOutboxOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var indexScope = scopeFactory.CreateAsyncScope();
                await using (indexScope.ConfigureAwait(false))
                {
                    await indexScope.ServiceProvider.GetRequiredService<MongoEventOutbox>()
                        .EnsureIndexesAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Outbox index creation failed; delivery continues unindexed.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var claimedCount = 0;
                try
                {
                    claimedCount = await DeliverPendingAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // A polling pass failure (e.g. the database is down) must not kill the
                    // processor - the next pass retries.
                    logger?.LogError(exception, "Outbox delivery pass failed.");
                }

                // A full batch means more work is almost certainly pending: keep draining
                // without the poll delay, so a backlog drains at delivery throughput instead of
                // being capped at BatchSize per PollInterval.
                if (claimedCount >= options.BatchSize)
                    continue;

                try
                {
                    await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        async Task<int> DeliverPendingAsync(CancellationToken stoppingToken)
        {
            IReadOnlyList<OutboxEventDocument> claimed;
            var claimScope = scopeFactory.CreateAsyncScope();
            await using (claimScope.ConfigureAwait(false))
            {
                claimed = await claimScope.ServiceProvider.GetRequiredService<MongoEventOutbox>()
                    .ClaimPendingAsync(options.BatchSize, stoppingToken).ConfigureAwait(false);
            }

            foreach (var document in claimed)
            {
                // A scope per event: deferred handlers may rely on per-delivery scoped state, and
                // one event poisoning a scoped service must not affect the rest of the batch.
                var scope = scopeFactory.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var outbox = scope.ServiceProvider.GetRequiredService<MongoEventOutbox>();
                    var serializer = scope.ServiceProvider.GetRequiredService<IOutboxEventSerializer>();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

                    try
                    {
                        // Inside the per-event guard: a malformed payload (deserialization throwing)
                        // must be marked failed like any other delivery failure, not abort the pass
                        // and starve the co-claimed events.
                        var @event = serializer.Deserialize(document.EventType, document.Payload);
                        if (@event == null)
                        {
                            logger?.LogError("Outbox event {EventId} has unresolvable type \"{EventType}\".", document.Id, document.EventType);
                            await outbox.MarkFailedAsync(document.Id, document.Attempts, $"Unresolvable event type \"{document.EventType}\".", CancellationToken.None).ConfigureAwait(false);
                            continue;
                        }

                        await dispatcher.DispatchDeferredAsync(@event, stoppingToken).ConfigureAwait(false);

                        // The delivery is decided: acknowledging is post-decision work - a shutdown
                        // mid-ack would redeliver an already-handled event after the lease expires.
                        await outbox.MarkDeliveredAsync(document.Id, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger?.LogError(exception, "Outbox event {EventId} ({EventType}) delivery failed (attempt {Attempt}).", document.Id, document.EventType, document.Attempts);
                        await outbox.MarkFailedAsync(document.Id, document.Attempts, exception.Message, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }

            return claimed.Count;
        }
    }
}
