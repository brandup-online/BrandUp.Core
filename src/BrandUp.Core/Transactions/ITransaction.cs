namespace BrandUp.Transactions
{
    /// <summary>
    /// Begins a unit-of-work on the ambient storage session. Implemented by storage packages
    /// (e.g. a MongoDB session wrapper) and consumed by <see cref="TransactionBehavior"/>.
    /// </summary>
    public interface ITransactionFactory
    {
        /// <summary>
        /// Starts a transaction. Called for every transactional command dispatch, including nested
        /// ones: when a transaction is already active on the ambient session, the implementation
        /// must return a nested handle whose <see cref="ITransaction.CommitAsync"/> and abort are
        /// no-ops, so the outermost handle owns the actual commit.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A unit-of-work that commits explicitly via <see cref="CommitAsync"/> or aborts on disposal.
    /// </summary>
    public interface ITransaction : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Commits the transaction. Not calling this before disposal aborts it.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task CommitAsync(CancellationToken cancellationToken = default);
    }
}
