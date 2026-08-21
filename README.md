# BrandUp.Core

Base framework for .NET development: a universal `Result` structure and lightweight CQRS infrastructure — queries, commands, items and domain events — on top of `Microsoft.Extensions.DependencyInjection`.

* **Results** — success-or-errors outcome for every operation.
* **Queries** — read operations returning a list of rows or a single value.
* **Commands** — write operations, with or without result data, validated before execution.
* **Items** — domain entities resolved by identifier, with commands targeting a loaded item.
* **Events** — facts published by commands and routed to any number of handlers, immediately or after the command completes.

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

`ILogger` extension `logger.LogIfError(result)` writes the errors of a failed result and reports whether there were any.

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
