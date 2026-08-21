using BrandUp.Commands;
using BrandUp.Events;
using BrandUp.Items;
using BrandUp.Queries;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BrandUp
{
    internal class DomainImpl(IOptions<DomainOptions> options, IServiceProvider serviceProvider, DomainEventPublisher eventPublisher) : IDomain
    {
        readonly DomainOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        readonly DomainEventPublisher eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));

        #region IDomain members

        public TItemProvider GetItemProvider<TItemProvider>()
        {
            return serviceProvider.GetService<TItemProvider>() ?? throw new InvalidOperationException($"Not found {typeof(TItemProvider).FullName} item provider.");
        }

        public async Task<TItem?> FindItemAsync<TId, TItem>(TId itemId, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            var itemProvider = serviceProvider.GetRequiredService<IItemProvider<TId, TItem>>();
            return await itemProvider.FindByIdAsync(itemId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<IList<TRow>>> QueryAsync<TRow>(IQuery<TRow> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var queryType = query.GetType();
            if (!options.TryGetQueryHandler(queryType, out QueryMetadata? queryMetadata))
                throw new InvalidOperationException($"Not found query handler by type \"{queryType.AssemblyQualifiedName}\"");
            if (queryMetadata.IsSingle)
                throw new InvalidOperationException($"Query \"{queryType.AssemblyQualifiedName}\" returns a single value. Use QueryAsync<TResult>(ISingleQuery<TResult>).");

            var validationResult = ValidateObj(query, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<IList<TRow>>();

            var handlerObject = queryMetadata.CreateHandler(serviceProvider);
            try
            {
                var rows = await ((Task<IList<TRow>>)queryMetadata.Invoke(handlerObject, query, cancellationToken)).ConfigureAwait(false);

                return Result.Success(rows);
            }
            finally
            {
                await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
            }
        }

        public async Task<Result<TModel>> QueryAsync<TModel>(ISingleQuery<TModel> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var queryType = query.GetType();
            if (!options.TryGetQueryHandler(queryType, out QueryMetadata? queryMetadata))
                throw new InvalidOperationException($"Not found query handler by type \"{queryType.AssemblyQualifiedName}\"");
            if (!queryMetadata.IsSingle)
                throw new InvalidOperationException($"Query \"{queryType.AssemblyQualifiedName}\" returns a list. Use QueryAsync<TRow>(IQuery<TRow>).");

            var validationResult = ValidateObj(query, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TModel>();

            var handlerObject = queryMetadata.CreateHandler(serviceProvider);
            try
            {
                return await ((Task<Result<TModel>>)queryMetadata.Invoke(handlerObject, query, cancellationToken)).ConfigureAwait(false);
            }
            finally
            {
                await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
            }
        }

        public async Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");
            if (commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled with a result. Use SendAsync<TResult>.");

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult;

            return await ExecuteCommandAsync<Result>(commandMetadata, null, command, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");
            if (!commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled without a result. Use SendAsync.");

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TResultData>();

            return await ExecuteCommandAsync<Result<TResultData>>(commandMetadata, null, command, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result> SendItemAsync<TId, TItem>(IItem<TId> item, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");
            if (commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled with a result. Use SendItemAsync<TId, TItem, TResult>.");

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult;

            return await ExecuteCommandAsync<Result>(commandMetadata, item, command, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(IItem<TId> item, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");
            if (!commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled without a result. Use SendItemAsync<TId, TItem>.");

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TResultData>();

            return await ExecuteCommandAsync<Result<TResultData>>(commandMetadata, item, command, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        static Result ValidateObj(object obj, IServiceProvider serviceProvider)
        {
            var errors = new List<CommandValidationError>();

            foreach (var validator in serviceProvider.GetServices<IValidator>())
                validator.Validate(obj, serviceProvider, errors);

            if (errors.Count > 0)
                return Result.Error(errors);

            return Result.Success();
        }

        async Task<TResult> ExecuteCommandAsync<TResult>(CommandMetadata commandMetadata, object? item, object command, CancellationToken cancellationToken)
            where TResult : Result
        {
            var handlerObject = commandMetadata.CreateHandler(serviceProvider);

            // With no deferred handlers registered the command scope is provably inert - skip
            // its allocations and AsyncLocal writes entirely.
            var withEventScope = options.HasDeferredEventHandlers;
            if (!withEventScope)
            {
                try
                {
                    return await ((Task<TResult>)commandMetadata.Invoke(handlerObject, item, command, cancellationToken)).ConfigureAwait(false);
                }
                finally
                {
                    await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
                }
            }

            eventPublisher.BeginCommand();
            var success = false;
            try
            {
                var result = await ((Task<TResult>)commandMetadata.Invoke(handlerObject, item, command, cancellationToken)).ConfigureAwait(false);
                success = result is { IsSuccess: true };
                return result;
            }
            finally
            {
                var disposed = false;
                try
                {
                    await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
                    disposed = true;
                }
                finally
                {
                    // Runs even when handler dispose throws: an unbalanced command scope would
                    // otherwise silently break deferred events for the rest of the async flow.
                    // A throwing dispose surfaces as an exception to the caller, so the deferred
                    // events are discarded to match what the caller observes.
                    await eventPublisher.EndCommandAsync(success && disposed).ConfigureAwait(false);
                }
            }
        }
    }
}
