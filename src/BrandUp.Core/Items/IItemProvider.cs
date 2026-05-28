namespace BrandUp.Items
{
    /// <summary>
    /// Resolves items of type <typeparamref name="TItem"/> by their identifier.
    /// </summary>
    /// <typeparam name="TId">Type of the identifier.</typeparam>
    /// <typeparam name="TItem">Type of the provided item.</typeparam>
    public interface IItemProvider<TId, TItem>
        where TItem : class, IItem<TId>
    {
        /// <summary>
        /// Finds an item by its identifier.
        /// </summary>
        /// <param name="itemId">Identifier of the item.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The item, or <see langword="null"/> if no item has the given identifier.</returns>
        Task<TItem?> FindByIdAsync(TId itemId, CancellationToken cancellationToken = default);
    }
}
