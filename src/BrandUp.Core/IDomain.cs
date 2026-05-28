using BrandUp.Commands;
using BrandUp.Items;
using BrandUp.Queries;

namespace BrandUp
{
    /// <summary>
    /// Entry point for executing queries, commands and item commands against the configured domain.
    /// </summary>
    public interface IDomain
    {
        /// <summary>
        /// Resolves a registered item provider.
        /// </summary>
        /// <typeparam name="TItemProvider">Type of the item provider to resolve.</typeparam>
        /// <returns>The item provider instance.</returns>
        /// <exception cref="InvalidOperationException">The provider is not registered.</exception>
        TItemProvider GetItemProvider<TItemProvider>();

        /// <summary>
        /// Finds an item by its identifier using the registered item provider.
        /// </summary>
        /// <typeparam name="TId">Type of the identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <param name="itemId">Identifier of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The item, or <see langword="null"/> if not found.</returns>
        Task<TItem?> FindItemAsync<TId, TItem>(TId itemId, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>;

        /// <summary>
        /// Validates and executes a query, returning its rows.
        /// </summary>
        /// <typeparam name="TRow">Type of a single returned row.</typeparam>
        /// <param name="query">Query to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="Result{TData}"/> with the matching rows or validation errors.</returns>
        Task<Result<IList<TRow>>> QueryAsync<TRow>(IQuery<TRow> query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and executes a command that does not produce result data.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A success or error <see cref="Result"/>.</returns>
        Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and executes a command that produces result data.
        /// </summary>
        /// <typeparam name="TResultData">Type of the produced result data.</typeparam>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="Result{TData}"/> with the produced data or errors.</returns>
        Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and executes a command against the given item, without producing result data.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A success or error <see cref="Result"/>.</returns>
        Task<Result> SendItemAsync<TId, TItem>(IItem<TId> item, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>;

        /// <summary>
        /// Validates and executes a command against the given item, producing result data.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <typeparam name="TResultData">Type of the produced result data.</typeparam>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="Result{TData}"/> with the produced data or errors.</returns>
        Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(IItem<TId> item, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>;
    }
}
