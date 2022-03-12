using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Items
{
    public interface IItemProvider<TId, TItem>
        where TItem : class, IItem<TId>
    {
        Task<TItem> FindByIdASync(TId itemId, CancellationToken cancellationToken = default);
    }
}