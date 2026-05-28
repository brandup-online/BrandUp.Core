namespace BrandUp.Commands
{
    /// <summary>
    /// Handles a command executed against an item, without producing result data.
    /// </summary>
    /// <typeparam name="TItem">Type of the target item.</typeparam>
    /// <typeparam name="TCommand">Type of the handled command.</typeparam>
    public interface IItemCommandHandler<in TItem, in TCommand>
        where TCommand : IItemCommand<TItem>
    {
        /// <summary>
        /// Executes the command against the item.
        /// </summary>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A success or error <see cref="Result"/>.</returns>
        Task<Result> HandleAsync(TItem item, TCommand command, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Handles a command executed against an item, producing result data of type <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of the target item.</typeparam>
    /// <typeparam name="TCommand">Type of the handled command.</typeparam>
    /// <typeparam name="TResult">Type of the produced result data.</typeparam>
    public interface IItemCommandHandler<in TItem, in TCommand, TResult>
        where TCommand : IItemCommand<TItem, TResult>
    {
        /// <summary>
        /// Executes the command against the item.
        /// </summary>
        /// <param name="item">Target item.</param>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="Result{TData}"/> with the produced data or errors.</returns>
        Task<Result<TResult>> HandleAsync(TItem item, TCommand command, CancellationToken cancellationToken = default);
    }
}
