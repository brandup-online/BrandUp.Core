namespace BrandUp.Queries
{
    /// <summary>
    /// Marker interface for a query that returns a single value of type <typeparamref name="TModel"/>.
    /// </summary>
    /// <typeparam name="TModel">
    /// Type of the returned value. Annotate as nullable (e.g. <c>User?</c>) if the query may produce no result.
    /// </typeparam>
    public interface ISingleQuery<out TModel>
    {
    }
}
