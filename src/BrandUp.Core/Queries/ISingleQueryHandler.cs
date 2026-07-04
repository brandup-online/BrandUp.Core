namespace BrandUp.Queries
{
    /// <summary>
    /// Handles a single-value query and returns a <see cref="Result{TData}"/>.
    /// </summary>
    /// <typeparam name="TQuery">Type of the handled query.</typeparam>
    /// <typeparam name="TModel">Type of the returned value.</typeparam>
    public interface ISingleQueryHandler<in TQuery, TModel>
        where TQuery : ISingleQuery<TModel>
    {
        /// <summary>
        /// Executes the query.
        /// </summary>
        /// <param name="query">Query to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A <see cref="Result{TData}"/> with the produced value or errors.</returns>
        Task<Result<TModel>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
    }
}
