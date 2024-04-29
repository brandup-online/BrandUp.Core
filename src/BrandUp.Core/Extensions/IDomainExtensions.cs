using BrandUp.Commands;
using BrandUp.Items;

namespace BrandUp
{
    public static class IDomainExtensions
    {
        public static async Task<Result> SendItemAsync<TId, TItem>(this IDomain domain, TId itemId, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(itemId);
            ArgumentNullException.ThrowIfNull(command);

            var itemProvider = domain.GetItemProvider<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdAsync(itemId, cancellationToken);
            if (item == null)
                return Result.Error(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await domain.SendItemAsync(item, command, cancellationToken);
        }

        public static async Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(this IDomain domain, TId itemId, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(itemId);
            ArgumentNullException.ThrowIfNull(command);

            var itemProvider = domain.GetItemProvider<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdAsync(itemId, cancellationToken);
            if (item == null)
                return Result.Error<TResultData>(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await domain.SendItemAsync(item, command, cancellationToken);
        }
    }
}