namespace BrandUp.Queries
{
    /// <summary>
    /// Handles a query and returns its rows.
    /// </summary>
    /// <typeparam name="TQuery">Type of the handled query.</typeparam>
    /// <typeparam name="TRow">Type of a single returned row.</typeparam>
    public interface IQueryHandler<in TQuery, TRow>
        where TQuery : IQuery<TRow>
    {
        /// <summary>
        /// Executes the query.
        /// </summary>
        /// <param name="query">Query to execute.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The list of matching rows.</returns>
        Task<IList<TRow>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
    }
}
