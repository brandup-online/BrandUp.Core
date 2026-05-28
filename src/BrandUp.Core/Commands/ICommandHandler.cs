namespace BrandUp.Commands
{
    /// <summary>
    /// Handles a command that does not produce result data.
    /// </summary>
    /// <typeparam name="TCommand">Type of the handled command.</typeparam>
    public interface ICommandHandler<in TCommand>
        where TCommand : ICommand
    {
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A success or error <see cref="Result"/>.</returns>
        Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Handles a command that produces result data of type <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TCommand">Type of the handled command.</typeparam>
    /// <typeparam name="TResult">Type of the produced result data.</typeparam>
    public interface ICommandHandler<in TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="Result{TData}"/> with the produced data or errors.</returns>
        Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }
}
