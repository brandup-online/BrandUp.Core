using System.Text;
using BrandUp.Behaviors;
using BrandUp.Caching;
using BrandUp.Events;
using BrandUp.Idempotency;
using BrandUp.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BrandUp
{
    /// <summary>
    /// Startup-time configuration checks behind
    /// <see cref="DomainBuilderExtensions.ValidateOnStart"/>: misconfigurations that would
    /// otherwise surface on the first dispatch fail when the options are first resolved (at host
    /// start under the options <c>ValidateOnStart</c> mechanism). Holds the live service
    /// collection, so registrations made after the call are still seen.
    /// </summary>
    sealed class DomainOptionsStartupValidator(IServiceCollection services) : IValidateOptions<DomainOptions>
    {
        public ValidateOptionsResult Validate(string? name, DomainOptions options)
        {
            List<string>? failures = null;

            if (options.UseEventOutbox && !HasService(typeof(IEventOutbox)))
                (failures ??= []).Add("The event outbox is enabled (UseEventOutbox) but no IEventOutbox is registered; deferred events would fail at publish time.");

            if (HasBehavior(typeof(TransactionBehavior)) && !HasService(typeof(ITransactionFactory)))
                (failures ??= []).Add("AddTransactions() wraps commands in transactions but no ITransactionFactory is registered; the first command dispatch would fail.");

            if (!HasBehavior(typeof(QueryCacheBehavior)))
            {
                var cachedQueries = CollectMarked(options.QueryTypes, typeof(ICachedQuery));
                if (cachedQueries != null)
                    (failures ??= []).Add($"Queries declare ICachedQuery but AddQueryCaching() is not called - they would silently run uncached: {cachedQueries}.");
            }

            if (!HasBehavior(typeof(IdempotencyBehavior)))
            {
                // The most dangerous silent marker: it looks like a deduplication guarantee.
                var idempotentCommands = CollectMarked(options.CommandTypes, typeof(IIdempotentCommand));
                if (idempotentCommands != null)
                    (failures ??= []).Add($"Commands declare IIdempotentCommand but AddIdempotency() is not called - they would silently run without deduplication: {idempotentCommands}.");
            }

            if (!HasBehavior(typeof(TimeoutBehavior)))
            {
                var timedRequests = CollectMarked(options.QueryTypes.Concat(options.CommandTypes), typeof(IDispatchTimeout));
                if (timedRequests != null)
                    (failures ??= []).Add($"Requests declare IDispatchTimeout but AddDispatchTimeouts() is not called - they would silently run without a timeout: {timedRequests}.");
            }

            return failures == null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        static StringBuilder? CollectMarked(IEnumerable<Type> requestTypes, Type markerType)
        {
            StringBuilder? marked = null;
            foreach (var requestType in requestTypes)
            {
                if (markerType.IsAssignableFrom(requestType))
                    (marked ??= new StringBuilder()).Append(marked.Length == 0 ? null : ", ").Append(requestType.FullName);
            }

            return marked;
        }

        bool HasService(Type serviceType)
        {
            return services.Any(descriptor => descriptor.ServiceType == serviceType && !descriptor.IsKeyedService);
        }

        bool HasBehavior(Type behaviorType)
        {
            return services.Any(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IDomainBehavior) && descriptor.ImplementationType == behaviorType);
        }
    }
}
