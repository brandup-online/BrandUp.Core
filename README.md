# BrandUp.Core

Base framework for .NET development: a universal `Result` structure and lightweight CQRS infrastructure — queries, commands, items and domain events — on top of `Microsoft.Extensions.DependencyInjection`.

* **Results** — success-or-errors outcome for every operation, with typed error kinds and functional combinators.
* **Queries** — read operations returning a list of rows or a single value, with optional caching.
* **Commands** — write operations, with or without result data, validated before execution and optionally wrapped in transactions.
* **Items** — domain entities resolved by identifier, with commands targeting a loaded item.
* **Events** — facts published by commands and routed to any number of handlers, immediately, after the command completes, or through a transactional outbox.
* **Behaviors** — middleware around every dispatch for cross-cutting concerns.
* **Observability** — OpenTelemetry-compatible spans and metrics out of the box.
* **BrandUp.Core.AspNetCore** — maps `Result` to HTTP responses and `ProblemDetails`.

## Getting started

```csharp
services.AddDomain(options =>
    {
        options.AddQuery<UserByPhoneQueryHandler>();
        options.AddCommand<SignUpCommandHandler>();
        options.AddEvent<UserSignedUpNotificationHandler>();
    })
    .AddValidator<ComponentModelValidator>()
    .AddItemProvider<UserProvider>();
```

Handlers can also be discovered by scanning: `options.AddHandlersFrom(typeof(SignUpCommand).Assembly)` registers every query, command and event handler in the assembly, removing the "wrote a handler, forgot to register it" failure mode (an optional type filter excludes types from the scan).

`AddDomain` registers `IDomain` — the entry point for dispatching queries and commands — as a scoped service. Handlers are constructed per dispatch with constructor injection, and disposed after execution if they implement `IDisposable`/`IAsyncDisposable`. Every dispatched query and command first runs through the registered validators; `ComponentModelValidator` applies the standard `System.ComponentModel.DataAnnotations` attributes.

```csharp
using var scope = serviceProvider.CreateAsyncScope();
var domain = scope.ServiceProvider.GetRequiredService<IDomain>();
```

## Results

`Result` (and `Result<TData>`) is the outcome of every operation: success, or a non-empty set of errors.

```csharp
Result.Success();
Result.Success(data);                    // Result<TData>
Result.Error("code", "message");
Result.Error<TData>("code", "message");
Result.Error(errors);                    // IEnumerable<IError>

result.IsSuccess;
result.Errors;
result.Data;                             // Result<TData> only
```

Errors carry an optional semantic category (`ErrorKind`: `Validation`, `NotFound`, `Unauthorized`, `Forbidden`, `Conflict`, `Internal`) used by transport layers to map domain errors to protocol status codes:

```csharp
Result.Error("user-not-found", "User not found.", ErrorKind.NotFound);
```

Functional combinators chain operations without explicit `IsSuccess` ladders — a failed result passes its errors through:

```csharp
Result<decimal> price = LoadOrder(orderId)          // Result<Order>
    .Ensure(order => !order.Deleted, "deleted", "Order is deleted.", ErrorKind.Conflict)
    .Map(order => order.Total)
    .Bind(total => ApplyDiscount(total));           // Func<decimal, Result<decimal>>

string text = price.Match(value => $"total: {value}", errors => "failed");
```

Async variants (`MapAsync`, `BindAsync`) chain on `Task<Result<T>>`. `ILogger` extension `logger.LogIfError(result)` writes the errors of a failed result and reports whether there were any.

## Queries

A list query implements `IQuery<TRow>`, its handler `IQueryHandler<TQuery, TRow>`:

```csharp
public class UserByPhoneQuery : IQuery<User>
{
    [Required]
    public string Phone { get; set; }
}

public class UserByPhoneQueryHandler(IUserRepository userRepository) : IQueryHandler<UserByPhoneQuery, User>
{
    public async Task<IList<User>> HandleAsync(UserByPhoneQuery query, CancellationToken cancellationToken = default)
    {
        return await userRepository.FindByPhoneAsync(query.Phone, cancellationToken);
    }
}

Result<IList<User>> result = await domain.QueryAsync(new UserByPhoneQuery { Phone = "+79232229022" });
```

A single-value query implements `ISingleQuery<TModel>`, its handler `ISingleQueryHandler<TQuery, TModel>` and returns a `Result<TModel>`, so it can fail with domain errors:

```csharp
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

Result<int> count = await domain.QueryAsync(new UserCountQuery());
```

Both kinds are registered the same way: `options.AddQuery<THandler>()`.

### Query caching

A query declaring `ICachedQuery` (a cache key and an optional duration) is served from `IQueryCache` when `AddQueryCaching()` is enabled; only successful results are cached, and the cached `Result` instance is shared between callers — treat its data as read-only. A command declaring `ICacheInvalidating` removes the listed keys after it succeeds — always after the command's transaction commit: `AddQueryCaching` keeps the cache behavior outside `TransactionBehavior` regardless of registration order. A cached query dispatched from inside a command bypasses the cache entirely (it may observe uncommitted state), and a cache-key collision between different query shapes is treated as a miss. The default cache is the in-process `MemoryQueryCache` (backed by `Microsoft.Extensions.Caching.Memory` with expiration-driven eviction); register a custom `IQueryCache` for distributed scenarios.

```csharp
public class UserCountQuery : ISingleQuery<int>, ICachedQuery
{
    public string CacheKey => "user-count";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public class SignUpCommand : ICommand<SignUpResult>, ICacheInvalidating
{
    public IEnumerable<string> InvalidateCacheKeys => ["user-count"];
}

services.AddDomain(options => { /* ... */ })
    .AddQueryCaching();
```

## Commands

A command implements `ICommand` (no result data) or `ICommand<TResult>`; the handler returns `Result` / `Result<TResult>`:

```csharp
public class SignUpCommand : ICommand<SignUpResult>
{
    [Required]
    public string Phone { get; set; }
}

public class SignUpResult
{
    public User User { get; set; }
}

public class SignUpCommandHandler(IUserRepository userRepository) : ICommandHandler<SignUpCommand, SignUpResult>
{
    public async Task<Result<SignUpResult>> HandleAsync(SignUpCommand command, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Phone = command.Phone
        };

        await userRepository.CreateAsync(user, cancellationToken);

        return Result.Success(new SignUpResult { User = user });
    }
}

Result<SignUpResult> signUpResult = await domain.SendAsync(new SignUpCommand { Phone = "+79231145449" });
```

Validation errors are returned as a failed `Result` without invoking the handler. Registration: `options.AddCommand<THandler>()` — one handler per command type.

### Transactions

`AddTransactions<TFactory>()` wraps every command (queries are skipped) in a unit-of-work from `ITransactionFactory`: begin before the handler, commit on a successful result, abort on an error result or exception. Combined with deferred events this makes the guarantee strict — deferred handlers flush only after the commit.

```csharp
public class MongoTransactionFactory(MongoDbSession session) : ITransactionFactory
{
    public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default) => /* wrap session */;
}

services.AddDomain(options => { /* ... */ })
    .AddTransactions<MongoTransactionFactory>();
```

The factory is called for nested commands too, so inside an active transaction it must return a nested handle whose commit is a no-op. A command that manages transactions itself (e.g. a long-running batch committing in chunks) opts out with `[NonTransactional]` on the command class.

### Behaviors

A behavior is middleware around every dispatched query and command: logging with timings, metrics, retry, idempotency, authorization — without changing the core. Behaviors run in registration order (the first registered is the outermost) after the built-in validation behavior; validation itself is just the first behavior of the pipeline. Transactions and query caching above are behaviors too.

```csharp
public class TimingBehavior(ILogger<TimingBehavior> logger) : IDomainBehavior
{
    public async Task<Result> InvokeAsync(DomainBehaviorContext context, DomainBehaviorDelegate next, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await next();
        logger.LogInformation("{Kind} {Request}: {Elapsed}", context.Kind, context.Request.GetType().Name, Stopwatch.GetElapsedTime(started));
        return result;
    }
}

services.AddDomain(options => { /* ... */ })
    .AddBehavior<TimingBehavior>();
```

A behavior may short-circuit by returning `context.CreateError(...)` — the context shapes the error to the exact result type the dispatch expects.

## Items

An item is a domain entity with an identifier (`IItem<TId>`), resolved through a registered `IItemProvider<TId, TItem>`:

```csharp
public class User : IItem<Guid>
{
    public Guid Id { get; set; }
    public string Phone { get; set; }
}

public class UserProvider(IUserRepository userRepository) : IItemProvider<Guid, User>
{
    public Task<User?> FindByIdAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return userRepository.FindByIdAsync(itemId, cancellationToken);
    }
}
```

An item command targets a loaded item — the handler receives the item together with the command:

```csharp
public class VisitUserCommand : IItemCommand<User>
{
}

public class VisitUserCommandHandler : IItemCommandHandler<User, VisitUserCommand>
{
    public Task<Result> HandleAsync(User item, VisitUserCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}
```

Dispatch with an already-loaded item, or by identifier — the by-id overloads resolve the item through its provider first and turn a missing item into an error `Result`:

```csharp
User? user = await domain.FindItemAsync<Guid, User>(userId);
if (user != null)
    await domain.SendItemAsync(user, new VisitUserCommand());

// or by id — a missing item becomes an error Result:
Result result = await domain.SendItemAsync(userId, new VisitUserCommand());
```

Item commands with result data (`IItemCommand<TItem, TResult>` / `IItemCommandHandler<TItem, TCommand, TResult>`) work the same way. Providers are also resolvable directly: `domain.GetItemProvider<UserProvider>()`, or `IItemProvider<Guid, User>` from the service provider.

## Events

A domain event is a fact that occurred in the domain. Commands publish facts through `IDomainEventPublisher`; what each fact spawns is known by its registered handlers, not by the command that raised it. An event may have any number of handlers (zero is valid), executed sequentially in registration order; events are routed by their runtime type (exact match). One handler class may handle several event types.

```csharp
public class UserSignedUp : IDomainEvent
{
    public required User User { get; init; }
}

public class UserSignedUpNotificationHandler(INotificationService notificationService) : IDomainEventHandler<UserSignedUp>
{
    public Task HandleAsync(UserSignedUp @event, CancellationToken cancellationToken = default)
    {
        return notificationService.WelcomeAsync(@event.User, cancellationToken);
    }
}

public class SignUpCommandHandler(IUserRepository userRepository, IDomainEventPublisher eventPublisher) : ICommandHandler<SignUpCommand, SignUpResult>
{
    public async Task<Result<SignUpResult>> HandleAsync(SignUpCommand command, CancellationToken cancellationToken = default)
    {
        // ...

        await eventPublisher.PublishAsync(new UserSignedUp { User = user }, cancellationToken);

        return Result.Success(result);
    }
}

services.AddDomain(options =>
{
    options.AddCommand<SignUpCommandHandler>();
    options.AddEvent<UserSignedUpNotificationHandler>();
    options.AddEvent<UserSignedUpAnalyticsHandler>();

    // Optionally, treat an event without handlers as a configuration error:
    // options.RequireEventHandlers = true;
});
```

### Deferred handlers

A handler implementing `IDeferredDomainEventHandler<TEvent>` runs after the outermost domain command completes successfully — no manual flushing required. If the command (or any enclosing command) fails with an error `Result` or an exception, the deferred execution is discarded: the fact did not survive. Published outside of a command, a deferred handler runs immediately.

```csharp
// Runs after the command completes successfully; discarded if it fails.
public class UserSignedUpAnalyticsHandler(IAnalyticsService analyticsService) : IDeferredDomainEventHandler<UserSignedUp>
{
    public Task HandleAsync(UserSignedUp @event, CancellationToken cancellationToken = default)
    {
        return analyticsService.TrackSignUpAsync(@event.User, cancellationToken);
    }
}
```

Error semantics differ by kind: an exception from an **immediate** handler propagates to the publishing command; an exception from a **deferred** handler is logged and neither fails the already-completed command nor stops the remaining deferred handlers. Deferred handlers run with `CancellationToken.None` — by then the command has succeeded and its caller may already be gone, so a handler needing a timeout manages its own. Parallel and nested command dispatches keep independent deferred queues, so concurrent commands never observe each other's events.

### Transactional outbox

In-process deferred execution loses the fact when a deferred handler keeps failing. With the outbox enabled — explicitly, via `AddEventOutbox` (merely registering an `IEventOutbox` implementation changes nothing) — deferred events are instead persisted at publish time, inside the command's transaction when the store shares it, and a background processor delivers them after commit with retries:

```csharp
// enqueue side: explicit opt-in, registers the store and reroutes deferred events
services.AddDomain(options => { /* ... */ })
    .AddEventOutbox<MongoEventOutbox>();

// delivery side: what the background processor does with a stored event
await domainEventDispatcher.DispatchDeferredAsync(storedEvent, cancellationToken);
```

Immediate handlers are unaffected. `IDomainEventDispatcher` propagates handler exceptions so the processor can retry — delivery is at-least-once, handlers must be idempotent. The outbox is designed to be paired with `AddTransactions`: without a transaction, events enqueued by a command that later fails are delivered anyway.

## Observability

Dispatch is instrumented with `System.Diagnostics` out of the box — no configuration, no cost without listeners. Subscribe OpenTelemetry (or any listener) to the `BrandUp.Domain` activity source and meter:

* a span per query/command dispatch (`domain.query`, `domain.command`, ...) tagged with the request type and outcome, and a span per event handler execution;
* histograms `brandup.domain.dispatch.duration` and `brandup.domain.event_handler.duration` (ms), tagged with kind/type/status.

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(DomainDiagnostics.ActivitySourceName))
    .WithMetrics(metrics => metrics.AddMeter(DomainDiagnostics.MeterName));
```

## ASP.NET Core

The `BrandUp.Core.AspNetCore` package maps `Result` to HTTP responses, deriving the status code from the `ErrorKind` of the first error (Validation → 400, NotFound → 404, Unauthorized → 401, Forbidden → 403, Conflict → 409, Internal → 500):

```csharp
// minimal APIs
app.MapPost("/users", async (SignUpRequest request, IDomain domain) =>
{
    var result = await domain.SendAsync(new SignUpCommand { Phone = request.Phone });
    return result.ToHttpResult(); // 200 with data, or ProblemDetails with the mapped status
});

// MVC
return signUpResult.ToActionResult();

// just the ProblemDetails
ProblemDetails problem = result.ToProblemDetails();
```

Failed results become `ProblemDetails` with an `errors` extension listing code/message pairs; validation errors also carry their member names.

## Testing

The `BrandUp.Core.Testing` package (framework-agnostic) turns domain tests into one-liners; `BrandUp.Core.Testing.xUnit` makes failures surface as native xUnit assertion failures — referencing it is enough, or call `XunitDomainAssert.Use()` once to be explicit.

```csharp
await using var host = DomainTestHost.Create(
    options =>
    {
        options.AddCommand<SignUpCommandHandler>();
        options.CaptureEvent<UserSignedUp>();       // subscribe the event capture
    },
    builder => builder
        .AddTestTransactions()                       // fake ITransactionFactory recording begin/commit/abort
        .AddTestEventOutbox(),                       // fake IEventOutbox collecting deferred events
    services => services.AddSingleton<IUserRepository, InMemoryUserRepository>());

// dispatch + assert + get the data in one call
var signUp = await host.Domain.AssertSendAsync(new SignUpCommand { Phone = "+79231145449" });

// the error family asserts failure and returns the matched error
await host.Domain.AssertSendErrorAsync(new SignUpCommand { Phone = "" }, kind: ErrorKind.Validation);

// events, transactions, outbox
host.Events.AssertPublished<UserSignedUp>(e => e.User.Phone == "+79231145449");
Assert.Equal(["begin", "commit"], host.Transactions.Operations);
await host.Outbox.DeliverAsync(host.EventDispatcher);   // what a background processor would do

// plain Result assertions work anywhere
result.AssertSuccess();
result.AssertError("user-not-found", ErrorKind.NotFound);
```

Also included: `AssertQueryAsync` (list and single-value), `AssertSendItemAsync`/`AssertSendItemErrorAsync` (with sync and async checks over data and item), `AssertItemAsync`/`AssertNoItemAsync`. Failure messages list every error with its code and kind. The helpers throw `DomainAssertException` by default, so they work with any test framework; adapters replace the exception via `DomainAssert.UseExceptionFactory`.
