namespace BrandUp.Queries
{
    public interface IQueryHandler<in TQuery, TRow>
        where TQuery : IQuery<TRow>
    {
        Task<IList<TRow>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
    }
}