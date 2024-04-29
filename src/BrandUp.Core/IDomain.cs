using BrandUp.Commands;
using BrandUp.Items;
using BrandUp.Queries;

namespace BrandUp
{
    public interface IDomain
    {
        TItemProvider GetItemProvider<TItemProvider>();
        Task<TItem> FindItemAsync<TId, TItem>(TId itemId, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>;

        Task<Result<IList<TRow>>> QueryAsync<TRow>(IQuery<TRow> query, CancellationToken cancellationToken = default);

        Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default);
        Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancellationToken = default);

        Task<Result> SendItemAsync<TId, TItem>(IItem<TId> item, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>;
        Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(IItem<TId> item, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>;
    }
}