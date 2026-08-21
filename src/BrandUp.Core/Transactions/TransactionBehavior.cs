using System.Collections.Concurrent;
using BrandUp.Behaviors;

namespace BrandUp.Transactions
{
    /// <summary>
    /// Wraps every command dispatch (queries are skipped) in a transaction from the registered
    /// <see cref="ITransactionFactory"/>: begin before the handler, commit on a successful result,
    /// abort on an error result or exception. Commands marked <see cref="NonTransactionalAttribute"/>
    /// are dispatched outside the boundary. Registered via
    /// <see cref="DomainBuilderExtensions.AddTransactions(Builder.IDomainBuilder)"/>; combined with
    /// deferred domain events this makes the guarantee strict — deferred handlers flush only after
    /// the commit, since the event scope closes after the whole pipeline.
    /// </summary>
    public class TransactionBehavior(ITransactionFactory transactionFactory) : IDomainBehavior
    {
        static readonly ConcurrentDictionary<Type, bool> nonTransactionalCommands = new();

        readonly ITransactionFactory transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));

        /// <inheritdoc/>
        public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            if (!context.IsCommand || IsNonTransactional(context.Request.GetType()))
                return await next().ConfigureAwait(false);

            var transaction = await transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var result = await next().ConfigureAwait(false);

                if (result is { IsSuccess: true })
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                // Disposal aborts when CommitAsync was not reached (error result or exception).
                return result;
            }
        }

        static bool IsNonTransactional(Type commandType)
        {
            return nonTransactionalCommands.GetOrAdd(commandType, static type => type.IsDefined(typeof(NonTransactionalAttribute), inherit: true));
        }
    }
}
