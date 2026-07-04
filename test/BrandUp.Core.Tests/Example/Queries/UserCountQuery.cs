using BrandUp.Example.Items;
using BrandUp.Queries;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp.Example.Queries
{
    public class UserCountQuery : ISingleQuery<int>
    {
    }

    public class UserCountQueryHandler : ISingleQueryHandler<UserCountQuery, int>
    {
        public Task<Result<int>> HandleAsync(UserCountQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(5));
        }
    }

    // Another single query with the same TModel (int) as UserCountQuery - used to test
    // that single queries sharing a model type dispatch to their own handler.
    public class ActiveUserCountQuery : ISingleQuery<int>
    {
    }

    public class ActiveUserCountQueryHandler : ISingleQueryHandler<ActiveUserCountQuery, int>
    {
        public Task<Result<int>> HandleAsync(ActiveUserCountQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(3));
        }
    }

    public class MissingUserQuery : ISingleQuery<User>
    {
    }

    public class MissingUserQueryHandler : ISingleQueryHandler<MissingUserQuery, User>
    {
        public Task<Result<User>> HandleAsync(MissingUserQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Error<User>("not-found", "User not found"));
        }
    }
}
