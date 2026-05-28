namespace BrandUp.Commands
{
    /// <summary>
    /// Marker interface for a command executed against an item of type <typeparamref name="TItem"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of the target item.</typeparam>
    public interface IItemCommand<in TItem> : ICommand
    {
    }

    /// <summary>
    /// Marker interface for a command executed against an item of type <typeparamref name="TItem"/>
    /// that produces result data of type <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of the target item.</typeparam>
    /// <typeparam name="TResult">Type of the data returned by the command handler.</typeparam>
    public interface IItemCommand<in TItem, out TResult> : IItemCommand<TItem>
    {
    }
}
