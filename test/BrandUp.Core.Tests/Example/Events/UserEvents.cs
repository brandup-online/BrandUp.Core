using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BrandUp.Events;

namespace BrandUp.Example.Events
{
    public class UserJoined : IDomainEvent
    {
        public required string Phone { get; init; }
    }

    public class UserLeft : IDomainEvent
    {
        public required string Phone { get; init; }
    }

    /// <summary>Singleton probe collecting handler executions in order.</summary>
    public class EventLog
    {
        readonly List<string> entries = [];

        public IReadOnlyList<string> Entries => entries;

        // Locked: parallel-command tests append from concurrent flows.
        public void Add(string entry)
        {
            lock (entries)
                entries.Add(entry);
        }
    }

    public class UserJoinedFirstHandler(EventLog log) : IDomainEventHandler<UserJoined>
    {
        public Task HandleAsync(UserJoined @event, CancellationToken cancellationToken = default)
        {
            log.Add($"first:{@event.Phone}");
            return Task.CompletedTask;
        }
    }

    public class UserJoinedSecondHandler(EventLog log) : IDomainEventHandler<UserJoined>
    {
        public Task HandleAsync(UserJoined @event, CancellationToken cancellationToken = default)
        {
            log.Add($"second:{@event.Phone}");
            return Task.CompletedTask;
        }
    }

    public class UserJoinedDeferredHandler(EventLog log) : IDeferredDomainEventHandler<UserJoined>
    {
        public Task HandleAsync(UserJoined @event, CancellationToken cancellationToken = default)
        {
            log.Add($"deferred:{@event.Phone}");
            return Task.CompletedTask;
        }
    }

    public class UserEventsRouteHandler(EventLog log) : IDomainEventHandler<UserJoined>, IDomainEventHandler<UserLeft>
    {
        Task IDomainEventHandler<UserJoined>.HandleAsync(UserJoined @event, CancellationToken cancellationToken)
        {
            log.Add($"route-joined:{@event.Phone}");
            return Task.CompletedTask;
        }

        Task IDomainEventHandler<UserLeft>.HandleAsync(UserLeft @event, CancellationToken cancellationToken)
        {
            log.Add($"route-left:{@event.Phone}");
            return Task.CompletedTask;
        }
    }

    public class TokenProbeDeferredHandler(EventLog log) : IDeferredDomainEventHandler<UserJoined>
    {
        public Task HandleAsync(UserJoined @event, CancellationToken cancellationToken = default)
        {
            log.Add($"token-cancellable:{cancellationToken.CanBeCanceled}");
            return Task.CompletedTask;
        }
    }

    public class ThrowingDeferredHandler : IDeferredDomainEventHandler<UserJoined>
    {
        public Task HandleAsync(UserJoined @event, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Deferred handler failed.");
        }
    }

    public class DisposableEventHandler(Commands.DisposeProbe probe) : IDomainEventHandler<UserJoined>, IDisposable
    {
        public Task HandleAsync(UserJoined @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            probe.Disposed = true;
        }
    }
}
