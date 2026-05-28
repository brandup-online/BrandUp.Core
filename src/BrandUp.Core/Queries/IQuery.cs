namespace BrandUp.Queries
{
    /// <summary>
    /// Marker interface for a query that returns rows of type <typeparamref name="TRow"/>.
    /// </summary>
    /// <typeparam name="TRow">Type of a single returned row.</typeparam>
    public interface IQuery<out TRow>
    {
    }
}
