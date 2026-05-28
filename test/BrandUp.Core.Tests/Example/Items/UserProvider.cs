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

    // Always reports "not found" - used to test the not-found branch of SendItemAsync by id.
    public class NullUserProvider : IItemProvider<Guid, User>
    {
        public Task<User> FindByIdAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<User>(null);
        }
    }
}