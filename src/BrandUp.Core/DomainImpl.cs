using System.Diagnostics;
using BrandUp.Behaviors;
using BrandUp.Commands;
using BrandUp.Events;
using BrandUp.Items;
using BrandUp.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BrandUp
{
    internal sealed class DomainImpl(IOptions<DomainOptions> options, IServiceProvider serviceProvider, DomainEventPublisher eventPublisher) : IDomain
    {
        readonly DomainOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        readonly DomainEventPublisher eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        IDomainBehavior[]? behaviors;

        #region IDomain members

        public TItemProvider GetItemProvider<TItemProvider>()
        {
            return serviceProvider.GetService<TItemProvider>() ?? throw new InvalidOperationException($"Item provider \"{typeof(TItemProvider).FullName}\" is not registered.");
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
                throw new InvalidOperationException($"Query handler for query type \"{queryType.AssemblyQualifiedName}\" is not registered.");
            if (queryMetadata.IsSingle)
                throw new InvalidOperationException($"Query \"{queryType.AssemblyQualifiedName}\" returns a single value. Use QueryAsync<TResult>(ISingleQuery<TResult>).");

            var context = new DomainBehaviorContext(DomainDispatchKind.Query, query, null, serviceProvider, typeof(Result<IList<TRow>>), static errors => Result.Error<IList<TRow>>(errors), cancellationToken);

            return await DispatchAsync<Result<IList<TRow>>>(context, async () =>
            {
                var handlerObject = queryMetadata.CreateHandler(serviceProvider);
                try
                {
                    var rows = await ((Task<IList<TRow>>)queryMetadata.Invoke(handlerObject, query, context.CancellationToken)).ConfigureAwait(false);

                    return Result.Success(rows);
                }
                finally
                {
                    await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<TModel>> QueryAsync<TModel>(ISingleQuery<TModel> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var queryType = query.GetType();
            if (!options.TryGetQueryHandler(queryType, out QueryMetadata? queryMetadata))
                throw new InvalidOperationException($"Query handler for query type \"{queryType.AssemblyQualifiedName}\" is not registered.");
            if (!queryMetadata.IsSingle)
                throw new InvalidOperationException($"Query \"{queryType.AssemblyQualifiedName}\" returns a list. Use QueryAsync<TRow>(IQuery<TRow>).");

            var context = new DomainBehaviorContext(DomainDispatchKind.SingleQuery, query, null, serviceProvider, typeof(Result<TModel>), static errors => Result.Error<TModel>(errors), cancellationToken);

            return await DispatchAsync<Result<TModel>>(context, async () =>
            {
                var handlerObject = queryMetadata.CreateHandler(serviceProvider);
                try
                {
                    return await ((Task<Result<TModel>>)queryMetadata.Invoke(handlerObject, query, context.CancellationToken)).ConfigureAwait(false);
                }
                finally
                {
                    await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Command handler for command type \"{commandType.AssemblyQualifiedName}\" is not registered.");
            if (commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled with a result. Use SendAsync<TResult>.");

            var context = new DomainBehaviorContext(DomainDispatchKind.Command, command, null, serviceProvider, typeof(Result), static errors => Result.Error(errors), cancellationToken);

            return await ExecuteCommandAsync<Result>(commandMetadata, context, null, command, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Command handler for command type \"{commandType.AssemblyQualifiedName}\" is not registered.");
            if (!commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled without a result. Use SendAsync.");

            var context = new DomainBehaviorContext(DomainDispatchKind.Command, command, null, serviceProvider, typeof(Result<TResultData>), static errors => Result.Error<TResultData>(errors), cancellationToken);

            return await ExecuteCommandAsync<Result<TResultData>>(commandMetadata, context, null, command, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result> SendItemAsync<TId, TItem>(IItem<TId> item, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Command handler for command type \"{commandType.AssemblyQualifiedName}\" is not registered.");
            if (commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled with a result. Use SendItemAsync<TId, TItem, TResult>.");

            var context = new DomainBehaviorContext(DomainDispatchKind.ItemCommand, command, item, serviceProvider, typeof(Result), static errors => Result.Error(errors), cancellationToken);

            return await ExecuteCommandAsync<Result>(commandMetadata, context, item, command, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(IItem<TId> item, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetCommandHandler(commandType, out CommandMetadata? commandMetadata))
                throw new InvalidOperationException($"Command handler for command type \"{commandType.AssemblyQualifiedName}\" is not registered.");
            if (!commandMetadata.WithResult)
                throw new InvalidOperationException($"Command \"{commandType.AssemblyQualifiedName}\" is handled without a result. Use SendItemAsync<TId, TItem>.");

            var context = new DomainBehaviorContext(DomainDispatchKind.ItemCommand, command, item, serviceProvider, typeof(Result<TResultData>), static errors => Result.Error<TResultData>(errors), cancellationToken);

            return await ExecuteCommandAsync<Result<TResultData>>(commandMetadata, context, item, command, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        async Task<TResult> ExecuteCommandAsync<TResult>(CommandMetadata commandMetadata, DomainBehaviorContext context, object? item, object command, CancellationToken cancellationToken)
            where TResult : Result
        {
            async Task<Result> InvokeHandlerAsync()
            {
                var handlerObject = commandMetadata.CreateHandler(serviceProvider);
                try
                {
                    return await ((Task<TResult>)commandMetadata.Invoke(handlerObject, item, command, context.CancellationToken)).ConfigureAwait(false);
                }
                finally
                {
                    await HandlerActivator.DisposeHandlerAsync(handlerObject).ConfigureAwait(false);
                }
            }

            // Marks the async flow as inside a command (queries dispatched by the handler
            // bypass caching of possibly-uncommitted state) and collects work deferred until the
            // outermost command completes - e.g. cache invalidations, which must run after the
            // transaction commit regardless of behavior order.
            var dispatchScope = CommandDispatchScope.Begin();
            var success = false;

            // With no deferred handlers registered the event scope is provably inert - skip it.
            if (!options.HasDeferredEventHandlers)
            {
                try
                {
                    var result = await DispatchAsync<TResult>(context, InvokeHandlerAsync, cancellationToken).ConfigureAwait(false);
                    success = result is { IsSuccess: true };
                    return result;
                }
                finally
                {
                    await dispatchScope.CompleteAsync(success).ConfigureAwait(false);
                }
            }

            eventPublisher.BeginCommand();
            try
            {
                var result = await DispatchAsync<TResult>(context, InvokeHandlerAsync, cancellationToken).ConfigureAwait(false);
                success = result is { IsSuccess: true };
                return result;
            }
            finally
            {
                // Both run even when the pipeline throws (including a throwing handler dispose):
                // an unbalanced scope would silently break deferral for the rest of the async
                // flow. Everything deferred is discarded whenever the caller observes anything
                // but a successful result. The scopes close after the whole pipeline, so a
                // transaction behavior commits first; completion actions (cache invalidations)
                // run before deferred event handlers, which may re-read the invalidated keys.
                try
                {
                    await dispatchScope.CompleteAsync(success).ConfigureAwait(false);
                }
                finally
                {
                    await eventPublisher.EndCommandAsync(success).ConfigureAwait(false);
                }
            }
        }

        async Task<TResult> DispatchAsync<TResult>(DomainBehaviorContext context, DomainBehaviorDelegate handlerInvoke, CancellationToken cancellationToken)
            where TResult : Result
        {
            using var activity = DomainDiagnostics.StartDispatch(context);
            var startTimestamp = Stopwatch.GetTimestamp();

            // The behavior set is fixed for the scope's lifetime - resolve once per DomainImpl.
            var pipeline = behaviors ??= [.. serviceProvider.GetServices<IDomainBehavior>()];

            // First registered behavior is the outermost; index-walking shares one state
            // capture per dispatch instead of rebuilding a nested delegate chain (each step's
            // `next` delegate is still created lazily as the pipeline advances).
            Task<Result> InvokePipelineAsync(int index)
            {
                if (index >= pipeline.Length)
                    return handlerInvoke();

                var behavior = pipeline[index];
                return behavior.InvokeAsync(context, () => InvokePipelineAsync(index + 1), context.CancellationToken);
            }

            Result result;
            try
            {
                result = await InvokePipelineAsync(0).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                DomainDiagnostics.EndDispatch(activity, context, startTimestamp, exception: exception);
                throw;
            }

            if (result is not TResult typedResult)
            {
                var exception = new InvalidOperationException($"Dispatch of \"{context.Request.GetType().FullName}\" produced \"{result?.GetType().FullName ?? "null"}\" instead of \"{typeof(TResult).FullName}\". A behavior must return the result of next() or a result created via {nameof(DomainBehaviorContext)}.{nameof(DomainBehaviorContext.CreateError)}.");
                DomainDiagnostics.EndDispatch(activity, context, startTimestamp, exception: exception);
                throw exception;
            }

            DomainDiagnostics.EndDispatch(activity, context, startTimestamp, result);
            return typedResult;
        }
    }
}
