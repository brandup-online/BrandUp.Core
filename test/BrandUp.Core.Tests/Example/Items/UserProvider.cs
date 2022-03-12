using BrandUp.Items;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Items
{
    public class UserProvider : IItemProvider<Guid, User>
    {
        public Task<User> FindByIdAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new User { Id = itemId, Phone = "79232229022" });
        }
    }
}