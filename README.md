# BrandUp.Core

Base framework for .NET development: a universal `Result` structure and lightweight CQRS infrastructure — queries, commands, items and domain events — on top of `Microsoft.Extensions.DependencyInjection`.

* **Results** — success-or-errors outcome for every operation, with typed error kinds and functional combinators.
* **Queries** — read operations returning a list of rows or a single value, with optional caching.
* **Commands** — write operations, with or without result data, validated before execution and optionally wrapped in transactions.
* **Items** — domain entities resolved by identifier, with commands targeting a loaded item.
* **Events** — facts published by commands and routed to any number of handlers, immediately, after the command completes, or through a transactional outbox.
* **Behaviors** — middleware around every dispatch for cross-cutting concerns, with built-in authorization, dispatch timeouts and command idempotency.
* **Observability** — OpenTelemetry-compatible spans and metrics out of the box.
* **Error catalog** — cataloged error descriptors with stable codes, localized at the transport edge.
* **Testing** — `BrandUp.Core.Testing`/`.xUnit`: one-line domain test host, dispatch asserts, fakes.
* **BrandUp.Core.AspNetCore** — maps `Result` to RFC 9457 `ProblemDetails` responses (with `type`, `instance` and a `traceId` extension) and ships a `ProblemDetailsFactory` that gives MVC model validation and `Problem(...)`/`ValidationProblem(...)` the same uniform `errors` list.
* **BrandUp.Core.FluentValidation** — runs FluentValidation validators inside the domain validation pipeline.
* **BrandUp.Core.Outbox.MongoDB** — MongoDB transactional outbox with a background delivery processor.

## Getting started

```csharp
services.AddDomain(options =>
    {
        options.AddQuery<UserByPhoneQueryHandler>();
        options.AddCommand<SignUpCommandHandler>();
        options.AddEvent<UserSignedUpNotificationHandler>();
    })
    .AddItemProvider<UserProvider>();
```

Handlers can also be discovered by scanning: `options.AddHandlersFrom(typeof(SignUpCommand).Assembly)` registers every query, command and event handler in the assembly, removing the "wrote a handler, forgot to register it" failure mode (an optional type filter excludes types from the scan). `ValidateOnStart()` on the builder turns misconfigurations that would otherwise surface at the first dispatch — an enabled outbox without a store, `AddTransactions()` without a factory, and marker interfaces whose behavior is not registered (`ICachedQuery` without `AddQueryCaching()`, `IIdempotentCommand` without `AddIdempotency()`, `IDispatchTimeout` without `AddDispatchTimeouts()`) — into failures at host startup. Validation also materializes the whole handler configuration (reflection scans and compiled invokers), moving that one-time cost from the first request to host start.

`AddDomain` registers `IDomain` — the entry point for dispatching queries and commands — as a scoped service. Handlers are constructed per dispatch with constructor injection, and disposed after execution if they implement `IDisposable`/`IAsyncDisposable`. Every dispatched query and command first runs through the registered validators; the built-in `ComponentModelValidator` is registered by default, so `System.ComponentModel.DataAnnotations` attributes work out of the box. `AddValidator<TValidator>()` adds custom validators — the contract is asynchronous, so a validator can hit the database.

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

A query declaring `ICachedQuery` (a cache key and an optional duration) is served from `IQueryCache` when `AddQueryCaching()` is enabled; only successful results are cached, and the cached `Result` instance is shared between callers — treat its data as read-only. A command declaring `ICacheInvalidating` removes the listed keys after the **outermost** command completes — after the transaction commit, wherever the behavior sits in the pipeline; a nested command's invalidation waits for the outer command and is discarded when it fails. A cached query dispatched from inside a command bypasses the cache entirely (it may observe uncommitted state), and a cache-key collision between different query shapes is treated as a miss. The behavior takes its registration-order position like any other, and a cache hit short-circuits behaviors registered after it — call `AddQueryCaching()` after authorization-like behaviors. An invalidation failure after the commit is logged, never surfaced to the command's caller, and cached-query dispatches tag the dispatch span with `brandup.cache` (hit/miss/bypass). The default cache is the in-process `MemoryQueryCache` (backed by `Microsoft.Extensions.Caching.Memory`), bounded to 10 000 entries out of the box so parameterized cache keys (one per user, page, filter...) cannot grow the process without limit — `AddQueryCaching(cache => cache.SizeLimit = ...)` changes the bound (the limit counts entries, not bytes). Register a custom `IQueryCache` for distributed scenarios — its documented contract: infrastructure failures degrade to misses, they never fail dispatches.

Nothing ties `CacheKey` and `InvalidateCacheKeys` together automatically — keep the literals in one shared constants class so a renamed key cannot drift apart. `CacheDuration` has a default (`null`, no expiration) and only needs declaring when a TTL is wanted.

```csharp
public static class CacheKeys
{
    public const string UserCount = "user-count";
}

public class UserCountQuery : ISingleQuery<int>, ICachedQuery
{
    public string CacheKey => CacheKeys.UserCount;
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public class SignUpCommand : ICommand<SignUpResult>, ICacheInvalidating
{
    public IEnumerable<string> InvalidateCacheKeys => [CacheKeys.UserCount];
}

services.AddDomain(options => { /* ... */ })
    .AddQueryCaching();
```

For multi-instance deployments `AddDistributedQueryCaching()` stores results in the registered `IDistributedCache` (e.g. `AddStackExchangeRedisCache`), serialized by `IResultSerializer` — JSON by default: success data by its declared type, errors with their code, message, kind and format arguments (arguments round-trip as primitives); register a custom `IResultSerializer` to replace the format. A malformed or wrong-shaped payload reads as a miss, a backend outage degrades reads to misses and writes to no-ops, and deserialized results are fresh copies, so the shared-instance caveat above does not apply.

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

Validation errors are returned as a failed `Result` without invoking the handler; data-annotations validation is on by default, custom validators are added with `AddValidator<TValidator>()`. Teams on FluentValidation plug their rule sets in with the `BrandUp.Core.FluentValidation` package — the adapter runs every FluentValidation validator registered for the dispatched request type, reporting failures with their member names:

```csharp
services.AddValidatorsFromAssemblyContaining<SignUpCommandValidator>(); // FluentValidation.DependencyInjectionExtensions
services.AddDomain(options => { /* ... */ })
    .AddFluentValidation();
```

Registration: `options.AddCommand<THandler>()` — one handler per command type.

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

A behavior is middleware around every dispatched query and command: logging with timings, metrics, custom policies — without changing the core; the built-in authorization, timeout and idempotency stages below are ordinary behaviors registered the same way. Behaviors run in registration order (the first registered is the outermost) after the built-in validation behavior; validation itself is just the first behavior of the pipeline. Transactions and query caching above are behaviors too.

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

### Authorization

`AddAuthorization()` runs every `IDomainAuthorizer<TRequest>` registered for the dispatched request's exact type before the handler; a failed `Result` (typically `DomainErrors.AccessDenied` — `access-denied`, 403) short-circuits the pipeline. `AddAuthorizer<TAuthorizer>()` registers a class for every authorizer interface it implements. The one ordering rule that matters is enforced, not just documented: `AddAuthorization()` throws if `AddQueryCaching()` was already called, because a cache hit short-circuits later behaviors and would serve cached data without the check. For item commands the loaded item is available via `context.Item`, so resource-based checks need no second fetch.

```csharp
public class CancelOrderAuthorizer(ICurrentUser user) : IDomainAuthorizer<CancelOrderCommand>
{
    public Task<Result> AuthorizeAsync(CancelOrderCommand command, DomainBehaviorContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(user.CanManageOrders ? Result.Success() : Result.Error(DomainErrors.AccessDenied));
}

services.AddDomain(options => { /* ... */ })
    .AddAuthorization()                     // before AddQueryCaching — enforced
    .AddAuthorizer<CancelOrderAuthorizer>()
    .AddQueryCaching();
```

### Dispatch timeouts

With `AddDispatchTimeouts()`, a request declaring `IDispatchTimeout` is cancelled when its declared duration elapses, and the expiry surfaces as the cataloged `dispatch-timeout` error; the caller's own cancellation still throws. Without the registration the marker is inert. Downstream behaviors and the handler observe the narrowed token through `DomainBehaviorContext.CancellationToken`. Register before `AddTransactions()` so the transaction lives inside the timeout — a timed-out attempt aborts on disposal and its deferred events are discarded.

```csharp
public class BuildReportCommand : ICommand, IDispatchTimeout
{
    public TimeSpan Timeout => TimeSpan.FromSeconds(30);
}

services.AddDomain(options => { /* ... */ })
    .AddDispatchTimeouts()
    .AddTransactions<MongoTransactionFactory>();
```

### Idempotency

A command declaring `IIdempotentCommand` deduplicates by key: repeating a completed command replays its stored result without reaching the handler, and a dispatch while the key is in flight fails with `duplicate-request` (409). The key is marked completed only at the completion of the outermost command — after the transaction commit — so a rolled-back command never becomes replayable; a failed or throwing command releases the key for retry, and nested dispatches are exempt. The default store is the in-process `InMemoryIdempotencyStore` (1-hour retention, pruned by a background timer off the request path); an in-flight claim is bounded by the store's claim lease (10 minutes by default), so a crash or a lost completion frees the key instead of blocking it forever. Size the retention consciously: every completed key holds its full `Result` (including data) in memory for the whole period. Register a custom store with `AddIdempotency<TStore>()` for deduplication across instances; a replayed `Result` instance is shared between callers — treat its data as read-only.

```csharp
public class PayOrderCommand : ICommand<PaymentModel>, IIdempotentCommand
{
    public required string RequestId { get; init; }   // client-generated
    public string IdempotencyKey => RequestId;
}

services.AddDomain(options => { /* ... */ })
    .AddIdempotency()                       // before AddTransactions — a replay opens no transaction
    .AddTransactions<MongoTransactionFactory>();
```

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

// or by id — a missing item becomes the cataloged DomainErrors.ItemNotFound error
// ("item-not-found", ErrorKind.NotFound — 404 over HTTP):
Result result = await domain.SendItemAsync(userId, new VisitUserCommand());
```

The built-in error is an ordinary descriptor — register `catalog.AddFrom(typeof(DomainErrors))` to document and localize it alongside the domain's own codes. Item commands with result data (`IItemCommand<TItem, TResult>` / `IItemCommandHandler<TItem, TCommand, TResult>`) work the same way. Providers are also resolvable directly: `domain.GetItemProvider<UserProvider>()`, or `IItemProvider<Guid, User>` from the service provider.

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

In-process deferred execution loses the fact when a deferred handler keeps failing. With the outbox enabled — explicitly, via `AddEventOutbox<TStore>()`, or `UseEventOutbox()` when the store is registered elsewhere (merely registering an `IEventOutbox` implementation changes nothing) — deferred events are instead persisted at publish time, inside the command's transaction when the store shares it, and a background processor delivers them after commit with retries:

The `BrandUp.Core.Outbox.MongoDB` package is the ready-made implementation: `AddMongoEventOutbox()` registers the store, the JSON event serializer (round-tripping the event's runtime type) and a background processor with lease-based claims, retries and a poison state after `MaxAttempts`:

```csharp
services.AddDomain(options => { /* ... */ })
    .AddTransactions<MongoTransactionFactory>()
    .AddMongoEventOutbox(outbox =>
    {
        // join the command's transaction so a rolled-back command's events roll back too
        // (MongoDbSession comes from the BrandUp.MongoDB package)
        outbox.SessionAccessor = provider => provider.GetService<MongoDbSession>()?.Current;
        outbox.MaxAttempts = 8;
    });
```

Requires an `IMongoDatabase` in the container (or set `DatabaseAccessor`). A failed delivery is retried after its lease expires (`LeaseDuration`), so the lease doubles as the retry backoff; an event that exhausts `MaxAttempts` is marked dead and surfaces to dead-letter monitoring instead of being retried forever. The processor claims events in bulk (a few round trips per pass, not one per event), keeps draining without the poll delay while batches come back full — a backlog drains at delivery throughput, not at `BatchSize` per `PollInterval` — and provisions the claim indexes at startup, including a TTL index that expires delivered events after `DeliveredRetention` (7 days by default; dead events are kept for inspection).

A custom store for another database follows the same shape:

```csharp
// enqueue side: one store instance behind both its own type (for the processor's
// read-back API) and IEventOutbox, plus the explicit rerouting opt-in
services.AddScoped<SqlEventOutbox>();
services.AddScoped<IEventOutbox>(provider => provider.GetRequiredService<SqlEventOutbox>());
services.AddDomain(options => { /* ... */ })
    .UseEventOutbox();

// delivery side: a background processor reads stored events back and dispatches them
public class OutboxProcessor(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<SqlEventOutbox>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

            foreach (var stored in await store.ReadPendingAsync(stoppingToken))
            {
                try
                {
                    await dispatcher.DispatchDeferredAsync(stored.Event, stoppingToken);
                    await store.MarkDeliveredAsync(stored.Id, stoppingToken);
                }
                catch
                {
                    // left pending: retried on the next pass (at-least-once)
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

Immediate handlers are unaffected. `IDomainEventDispatcher` propagates handler exceptions so the processor can retry — delivery is at-least-once, handlers must be idempotent. The outbox is designed to be paired with `AddTransactions`: without a transaction, events enqueued by a command that later fails are delivered anyway.

`IEventOutbox` covers only the enqueue side by design. The store owns the rest of the contract: the schema (serialized payload plus the CLR type name, so the event's runtime type round-trips), the delivery state and attempt counter, the read-back API (`ReadPendingAsync`/`MarkDeliveredAsync` above) and the retry/poison policy.

## Observability

Dispatch is instrumented with `System.Diagnostics` out of the box — no configuration, no cost without listeners. Subscribe OpenTelemetry (or any listener) to the `BrandUp.Domain` activity source and meter:

* a span per query/command dispatch (`domain.query`, `domain.command`, ...) tagged with the request type and outcome — cached queries also carry a `brandup.cache` tag (hit/miss/bypass); a span per command transaction (`domain.transaction`, commit/abort outcome); and a span per event handler execution;
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

Failed results become RFC 9457 `ProblemDetails` served as `application/problem+json`: the mapped status, a `type` URI pointing at the RFC 9110 status definition, a `traceId` extension for correlating with logs and an `errors` extension listing uniform `ProblemError` items (`code`, `message` and, for validation errors with member names, `members`). Every mapping method also has an overload taking `HttpContext` that resolves the registered `IErrorLocalizer` from the request services and adds the request-bound defaults (`instance`, connection trace id) — endpoints don't need to inject the localizer themselves.

To make the rest of the API speak the same format, register the package's `ProblemDetailsFactory`:

```csharp
services.AddDomainProblemDetails();
```

With it, `[ApiController]` model validation, `Problem(...)` and `ValidationProblem(...)` produce the same shape as the `Result` mappings — validation problems carry the uniform `errors` list (model-state keys become `members`) instead of the framework's per-member dictionary, and every problem response gets the `type`, `instance` and `traceId` defaults. One contract for the whole API, whether the error came from a domain result, model binding or a handwritten `Problem(...)`.

The mapping cannot know where an identifier came from, so the `NotFound` kind is reserved by convention for the operation's primary subject — the entity the request addresses directly (over HTTP, typically by the URL). A missing entity that is only *referenced* from the request payload is a `Validation` error with a descriptive code (`channel-not-found`): at the transport level the request is wrong, not the addressed resource absent, and the response must be 400, not 404.

### Keeping an established error format

An API with an existing error contract does not have to adopt `ToProblemDetails`: it can keep its response shape and status mapping and take only the stable codes and localization from the domain. `AddToModelState` feeds a failed result into MVC model state so the controller answers with `ValidationProblem()` — the same `ValidationProblemDetails` shape as `[ApiController]` model validation (validation errors are keyed by member name, other errors by code).

`AddToModelState` ignores `ErrorKind` — `ValidationProblem()` is always 400 — so the kinds that are not about the request body must be routed to their own status first, otherwise an `Internal` failure reaches the client as a validation problem and is never counted as a 5xx. `ErrorKind.ToHttpStatusCode()` gives the same status the built-in mapping uses, so the API keeps its format without copying the mapping table:

```csharp
protected IActionResult DomainError(Result result)
{
    var kind = result.Errors.First().Kind;
    if (kind is not (ErrorKind.Validation or ErrorKind.Unspecified))
        return StatusCode(kind.ToHttpStatusCode(), result.ToProblemDetails(errorLocalizer));

    result.AddToModelState(ModelState, errorLocalizer); // localized messages, contract unchanged
    return ValidationProblem();                         // 400, the API's existing format
}
```

For a fully custom format, `error.LocalizeOrInvariant(errorLocalizer, culture)` (in the core package) applies the same localize-with-fallback rule the built-in mapping uses.

## Error catalog and localization

Domain errors are declared once, as descriptors in per-domain catalog classes — a stable code (the API contract), a semantic kind, an invariant developer-facing template and a description for integrators:

```csharp
public static class OrderErrors
{
    public static readonly ErrorDescriptor NotFound = new(
        "order-not-found", ErrorKind.NotFound,
        "Order {0} not found.",
        "The order id does not exist or belongs to another project.");
}

// in a handler: code, kind and message come from one place
return Result.Error(OrderErrors.NotFound, orderId);

services.AddDomain(options => { /* ... */ })
    .AddErrorCatalog(catalog => catalog
        .AddFromAssembly(typeof(OrderErrors).Assembly)
        .AddFrom(typeof(DomainErrors))); // built-in errors: item-not-found, access-denied,
                                         // dispatch-timeout, duplicate-request
```

The catalog validates code uniqueness at registration and feeds documentation: `app.MapErrorCatalog("/api/errors")` publishes every code with its kind, HTTP status, localized message and description. Per endpoint, `.ProducesDomainErrors(OrderErrors.NotFound, OrderErrors.AlreadyPaid)` declares which cataloged errors the operation can return — OpenAPI gets a `ProblemDetails` response per mapped status (derived from the same kind→status table the runtime mapping uses), and documentation tooling can read the descriptors back from the endpoint's `DomainErrorsMetadata`.

Localization happens at the transport layer, not at error creation: the domain carries the code, the invariant message and the format arguments (`IError.Arguments`), and the edge resolves a localized template per request culture. Cached results, stored outbox events and logs stay culture-free.

`MapErrorCatalog` and the `Result` mapping overloads live in the `BrandUp.Core.AspNetCore` package; the catalog, descriptors, `IErrorLocalizer` and its resx-backed `AddErrorLocalization` are core — localization does not depend on ASP.NET Core.

```csharp
// resx files keyed by error code: ErrorMessages.ru.resx -> "order-not-found" = "Заказ {0} не найден."
services.AddErrorLocalization<ErrorMessages>();
app.UseRequestLocalization(/* ... */);

// mapping overloads resolve messages by the request culture, falling back to the invariant
// text; the HttpContext overloads pick up the registered IErrorLocalizer automatically
return result.ToHttpResult(httpContext);
```

Templates resolve along the culture parent chain (`ru-RU` → `ru`) but never to the neutral resources — a code translated only in the default language reports as untranslated and falls back to the invariant message, and a malformed translated template falls back too instead of failing the response. A custom `IErrorLocalizer` replaces the resx-backed one for other template sources. The Testing package turns missing or broken translations into a failing test:

```csharp
ErrorCatalogAssert.AssertAllLocalized(catalog, errorLocalizer, "ru", "en");
result.AssertError(OrderErrors.NotFound); // refactoring-safe error asserts
```

Validation messages are a separate, already-solved path: DataAnnotations attributes localize themselves (resource-based error messages) at validation time with the request culture.

## Testing

The `BrandUp.Core.Testing` package (framework-agnostic) turns domain tests into one-liners; `BrandUp.Core.Testing.xUnit` makes failures surface as native xUnit assertion failures. The adapter installs itself when its assembly loads (a module initializer) — any use of its types guarantees that; call `XunitDomainAssert.Use()` once (e.g. in a fixture) to be deterministic.

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

Also included: `AssertQueryAsync` (list and single-value) with `AssertQueryErrorAsync` for fallible single-value queries, `AssertSendItemAsync`/`AssertSendItemErrorAsync` (in a resultless and an `IItemCommand<TItem, TResult>` form with checks over data and item — the `TResult` form also takes an async check — plus `ErrorDescriptor` overloads), `AssertItemAsync`/`AssertNoItemAsync`. Failure messages list every error with its code and kind. The helpers throw `DomainAssertException` by default, so they work with any test framework; adapters replace the exception via `DomainAssert.UseExceptionFactory`.
