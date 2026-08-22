using System.Collections.Concurrent;
using BrandUp.Behaviors;

namespace BrandUp.Transactions
{
    /// <summary>
    /// Wraps every command dispatch (queries are skipped) in a transaction from the registered
    /// <see cref="ITransactionFactory"/>: begin before the handler, commit on a successful result,
    /// abort on an error result or exception. Commands marked <see cref="NonTransactionalAttribute"/>
    /// are dispatched outside the boundary. Registered via
    /// <see cref="DomainBuilderExtensions.AddTransactions(IDomainBuilder)"/>; combined with
    /// deferred domain events this makes the guarantee strict — deferred handlers flush only after
    /// the commit, since the event scope closes after the whole pipeline.
    /// </summary>
    public sealed class TransactionBehavior(ITransactionFactory transactionFactory) : IDomainBehavior
    {
        static readonly ConcurrentDictionary<Type, bool> nonTransactionalCommands = new();

        readonly ITransactionFactory transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));

        /// <inheritdoc/>
        public Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            // Queries and non-transactional commands pass through with no async state machine.
            if (!context.IsCommand || IsNonTransactional(context.Request.GetType()))
                return next();

            return InvokeInTransactionAsync(context, next, cancellationToken);
        }

        async Task<Result> InvokeInTransactionAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken)
        {
            var activity = DomainDiagnostics.StartTransaction(context.Request.GetType());
            var outcome = "begin-failed";
            try
            {
                var transaction = await transactionFactory.BeginAsync(cancellationToken).ConfigureAwait(false);
                outcome = "abort";
                await using (transaction.ConfigureAwait(false))
                {
                    var result = await next().ConfigureAwait(false);

                    if (result is { IsSuccess: true })
                    {
                        // The result is decided: committing is post-decision work, not
                        // cancellable on the caller's behalf - a timeout firing mid-commit must
                        // not report a possibly-committed transaction as a definitive error.
                        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                        outcome = "commit";
                    }

                    // Disposal aborts when CommitAsync was not reached (error result or exception).
                    return result;
                }
            }
            finally
            {
                DomainDiagnostics.EndTransaction(activity, outcome);
            }
        }

        static bool IsNonTransactional(Type commandType)
        {
            return nonTransactionalCommands.GetOrAdd(commandType, static type => type.IsDefined(typeof(NonTransactionalAttribute), inherit: true));
        }
    }
}
