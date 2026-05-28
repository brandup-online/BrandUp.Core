namespace BrandUp.Items
{
    /// <summary>
    /// An entity identified by a value of type <typeparamref name="TId"/>.
    /// </summary>
    /// <typeparam name="TId">Type of the identifier.</typeparam>
    public interface IItem<out TId>
    {
        /// <summary>
        /// Identifier of the item.
        /// </summary>
        TId Id { get; }
    }
}
