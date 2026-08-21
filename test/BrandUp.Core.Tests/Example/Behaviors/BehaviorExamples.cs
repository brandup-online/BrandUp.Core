using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Behaviors;
using BrandUp.Caching;
using BrandUp.Commands;
using BrandUp.Events;
using BrandUp.Example.Events;
using BrandUp.Queries;
using BrandUp.Transactions;

namespace BrandUp.Example.Behaviors
{
    public class FirstLoggingBehavior(EventLog log) : IDomainBehavior
    {
        public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            log.Add("first:before");
            var result = await next();
            log.Add("first:after");
            return result;
        }
    }

    public class SecondLoggingBehavior(EventLog log) : IDomainBehavior
    {
        public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            log.Add("second:before");
            var result = await next();
            log.Add("second:after");
            return result;
        }
    }

    public class ShortCircuitBehavior : IDomainBehavior
    {
        public Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(context.CreateError("blocked", "Blocked by behavior.", ErrorKind.Forbidden));
        }
    }

    public class FakeTransactionFactory(EventLog log) : ITransactionFactory
    {
        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            log.Add("tx-begin");
            return Task.FromResult<ITransaction>(new FakeTransaction(log));
        }

        sealed class FakeTransaction(EventLog log) : ITransaction
        {
            bool committed;
            bool disposed;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                committed = true;
                log.Add("tx-commit");
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                if (!disposed && !committed)
                    log.Add("tx-abort");
                disposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    [NonTransactional]
    public class ManualTxCommand : ICommand
    {
    }

    public class ManualTxCommandHandler(EventLog log) : ICommandHandler<ManualTxCommand>
    {
        public Task<Result> HandleAsync(ManualTxCommand command, CancellationToken cancellationToken = default)
        {
            log.Add("manual-command");
            return Task.FromResult(Result.Success());
        }
    }

    public class CachedCountQuery : ISingleQuery<int>, ICachedQuery
    {
        public string CacheKey => "user-count";
        public TimeSpan? CacheDuration => null;
    }

    public class CachedCountQueryHandler(EventLog log) : ISingleQueryHandler<CachedCountQuery, int>
    {
        public Task<Result<int>> HandleAsync(CachedCountQuery query, CancellationToken cancellationToken = default)
        {
            log.Add("cached-query-exec");
            return Task.FromResult(Result.Success(42));
        }
    }

    public class InvalidateCountCommand : ICommand, ICacheInvalidating
    {
        public IEnumerable<string> InvalidateCacheKeys => ["user-count"];
    }

    public class InvalidateCountCommandHandler : ICommandHandler<InvalidateCountCommand>
    {
        public Task<Result> HandleAsync(InvalidateCountCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }
    }

    // Logs cache traffic so tests can assert ordering against transaction operations.
    public class RecordingQueryCache(EventLog log) : IQueryCache
    {
        public ValueTask<Result> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            log.Add($"cache-get:{key}");
            return ValueTask.FromResult<Result>(null);
        }

        public ValueTask SetAsync(string key, Result result, TimeSpan? duration, CancellationToken cancellationToken = default)
        {
            log.Add($"cache-set:{key}");
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            log.Add($"cache-remove:{key}");
            return ValueTask.CompletedTask;
        }
    }

    public class NestedCachedQueryCommand : ICommand
    {
    }

    // Dispatches a cached query from inside a command: the cache must be bypassed.
    public class NestedCachedQueryCommandHandler(IDomain domain, EventLog log) : ICommandHandler<NestedCachedQueryCommand>
    {
        public async Task<Result> HandleAsync(NestedCachedQueryCommand command, CancellationToken cancellationToken = default)
        {
            var queryResult = await domain.QueryAsync(new CachedCountQuery(), cancellationToken);

            log.Add($"command-query:{queryResult.Data}");

            return Result.Success();
        }
    }

    // Deliberately collides with CachedCountQuery's cache key while returning a different shape.
    public class CollidingUsersQuery : IQuery<Events.UserJoined>, ICachedQuery
    {
        public string CacheKey => "user-count";
        public TimeSpan? CacheDuration => null;
    }

    public class CollidingUsersQueryHandler : IQueryHandler<CollidingUsersQuery, Events.UserJoined>
    {
        public Task<IList<Events.UserJoined>> HandleAsync(CollidingUsersQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IList<Events.UserJoined>>([]);
        }
    }

    public class NestingInvalidateCommand : ICommand
    {
    }

    // Dispatches an ICacheInvalidating command from inside another command: the invalidation
    // must wait for the OUTER command's completion.
    public class NestingInvalidateCommandHandler(IDomain domain, EventLog log) : ICommandHandler<NestingInvalidateCommand>
    {
        public async Task<Result> HandleAsync(NestingInvalidateCommand command, CancellationToken cancellationToken = default)
        {
            var innerResult = await domain.SendAsync(new InvalidateCountCommand(), cancellationToken);

            log.Add("outer-command");

            return innerResult;
        }
    }

    // Queries a cached query from a deferred event handler - runs after the command completed,
    // so the cache must be served, not bypassed.
    public class QueryingDeferredHandler(IDomain domain, EventLog log) : IDeferredDomainEventHandler<UserJoined>
    {
        public async Task HandleAsync(UserJoined @event, CancellationToken cancellationToken = default)
        {
            var queryResult = await domain.QueryAsync(new CachedCountQuery(), cancellationToken);

            log.Add($"deferred-query:{queryResult.Data}");
        }
    }

    public class FakeEventOutbox : IEventOutbox
    {
        readonly List<IDomainEvent> events = [];

        public IReadOnlyList<IDomainEvent> Events => events;

        public Task EnqueueAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            lock (events)
                events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
