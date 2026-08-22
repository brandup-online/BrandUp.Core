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

            var context = new DomainBehaviorContext(DomainDispatchKind.Query, query, null, serviceProvider, typeof(Result<IList<TRow>>), static errors => new Result<IList<TRow>>(errors), cancellationToken);

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

            var context = new DomainBehaviorContext(DomainDispatchKind.SingleQuery, query, null, serviceProvider, typeof(Result<TModel>), static errors => new Result<TModel>(errors), cancellationToken);

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

            var context = new DomainBehaviorContext(DomainDispatchKind.Command, command, null, serviceProvider, typeof(Result), static errors => new Result(errors), cancellationToken);

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

            var context = new DomainBehaviorContext(DomainDispatchKind.Command, command, null, serviceProvider, typeof(Result<TResultData>), static errors => new Result<TResultData>(errors), cancellationToken);

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

            var context = new DomainBehaviorContext(DomainDispatchKind.ItemCommand, command, item, serviceProvider, typeof(Result), static errors => new Result(errors), cancellationToken);

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

            var context = new DomainBehaviorContext(DomainDispatchKind.ItemCommand, command, item, serviceProvider, typeof(Result<TResultData>), static errors => new Result<TResultData>(errors), cancellationToken);

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

            Result result;
            try
            {
                result = await new PipelineWalker(pipeline, context, handlerInvoke).InvokeNextAsync().ConfigureAwait(false);
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

        // One object and one delegate for the whole behavior walk: a mutable index replaces the
        // fresh closure the naive recursive form would capture per pipeline step. First
        // registered behavior is the outermost. next() is single-shot per behavior - a repeated
        // sequential invocation (a retry-style behavior this pipeline does not support) throws
        // loudly instead of resuming the walk past whatever short-circuited downstream, which
        // would silently run the handler without the short-circuiting check.
        sealed class PipelineWalker
        {
            readonly IDomainBehavior[] pipeline;
            readonly DomainBehaviorContext context;
            readonly DomainBehaviorDelegate handlerInvoke;
            readonly DomainBehaviorDelegate next;
            Task<Result>? lastStepTask;
            int index;

            public PipelineWalker(IDomainBehavior[] pipeline, DomainBehaviorContext context, DomainBehaviorDelegate handlerInvoke)
            {
                this.pipeline = pipeline;
                this.context = context;
                this.handlerInvoke = handlerInvoke;
                next = InvokeNextAsync;
            }

            public Task<Result> InvokeNextAsync()
            {
                // A legitimate next() always runs while the previous step's task is still
                // executing (or not yet materialized - the assignment below happens after the
                // step's InvokeAsync returned). A completed last task means a behavior invoked
                // its continuation again after awaiting the downstream chain's result.
                if (lastStepTask is { IsCompleted: true })
                    throw new InvalidOperationException("A behavior invoked next() more than once. next() may be invoked at most once per behavior.");

                var step = index++;
                if (step < pipeline.Length)
                    return lastStepTask = pipeline[step].InvokeAsync(context, next, context.CancellationToken);

                if (step > pipeline.Length)
                    throw new InvalidOperationException("A behavior invoked next() more than once. next() may be invoked at most once per behavior.");

                return handlerInvoke();
            }
        }
    }
}
