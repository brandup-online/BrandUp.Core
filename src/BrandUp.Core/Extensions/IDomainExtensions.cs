using BrandUp.Commands;
using BrandUp.Items;

namespace BrandUp
{
    /// <summary>
    /// <see cref="IDomain"/> convenience extensions that resolve the target item by id before dispatching.
    /// </summary>
    public static class IDomainExtensions
    {
        /// <summary>
        /// Loads the item by id and executes the command against it, without producing result data.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="itemId">Identifier of the target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A success or error <see cref="Result"/>; an error if the item is not found.</returns>
        public static async Task<Result> SendItemAsync<TId, TItem>(this IDomain domain, TId itemId, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(itemId);
            ArgumentNullException.ThrowIfNull(command);

            var itemProvider = domain.GetItemProvider<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdAsync(itemId, cancellationToken);
            if (item == null)
                return Result.Error(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await domain.SendItemAsync(item, command, cancellationToken);
        }

        /// <summary>
        /// Loads the item by id and executes the command against it, producing result data.
        /// </summary>
        /// <typeparam name="TId">Type of the item identifier.</typeparam>
        /// <typeparam name="TItem">Type of the item.</typeparam>
        /// <typeparam name="TResultData">Type of the produced result data.</typeparam>
        /// <param name="domain">Domain instance.</param>
        /// <param name="itemId">Identifier of the target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="Result{TData}"/> with the produced data or errors; an error if the item is not found.</returns>
        public static async Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(this IDomain domain, TId itemId, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(itemId);
            ArgumentNullException.ThrowIfNull(command);

            var itemProvider = domain.GetItemProvider<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdAsync(itemId, cancellationToken);
            if (item == null)
                return Result.Error<TResultData>(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await domain.SendItemAsync(item, command, cancellationToken);
        }
    }
}