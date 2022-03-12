using BrandUp.Commands;
using BrandUp.Items;
using BrandUp.Queries;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrandUp
{
    public class Domain : IDomain
    {
        readonly DomainOptions options;
        readonly IServiceProvider serviceProvider;

        public Domain(IOptions<DomainOptions> options, IServiceProvider serviceProvider)
        {
            this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        #region IDomain members

        public TItemProvider GetItemProvider<TItemProvider>()
        {
            return serviceProvider.GetRequiredService<TItemProvider>();
        }
        public Task<TItem> FindItem<TId, TItem>(TId itemId, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            var itemProvider = serviceProvider.GetRequiredService<IItemProvider<TId, TItem>>();
            return itemProvider.FindByIdASync(itemId, cancellationToken);
        }

        public async Task<Result<IList<TRow>>> QueryAsync<TRow>(IQuery<TRow> query, CancellationToken cancellationToken = default)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var queryType = query.GetType();
            if (!options.TryGetQueryHandler(queryType, out QueryMetadata queryMetadata))
                throw new InvalidOperationException($"Not found query handler by type \"{queryType.AssemblyQualifiedName}\"");

            using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(query, scope.ServiceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<IList<TRow>>();

            var handlerObject = CreateQueryHandler(queryMetadata, scope.ServiceProvider);

            var handlerTask = (Task<IList<TRow>>)queryMetadata.HandleMethod.Invoke(handlerObject, new object[] { query, cancellationToken });

            var rows = await handlerTask;

            return Result.Success(rows);
        }

        public async Task<Result> SendAsync(ICommand command, CancellationToken cancelationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var commandType = command.GetType();
            if (!options.TryGetHandlerNotResult(commandType, out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");

            using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, scope.ServiceProvider);
            if (!validationResult.IsSuccess)
                return validationResult;

            var handlerObject = CreateCommandHandler(commandMetadata, scope.ServiceProvider);

            var handlerTask = (Task<Result>)commandMetadata.HandleMethod.Invoke(handlerObject, new object[] { command, cancelationToken });

            return await handlerTask;
        }
        public async Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancelationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!options.TryGetHandlerWithResult<TResultData>(out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by result \"{typeof(TResultData).AssemblyQualifiedName}\".");

            using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, scope.ServiceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TResultData>();

            var handlerObject = CreateCommandHandler(commandMetadata, scope.ServiceProvider);

            var handlerTask = (Task<Result<TResultData>>)commandMetadata.HandleMethod.Invoke(handlerObject, new object[] { command, cancelationToken });

            return await handlerTask;
        }

        public async Task<Result> FindItemAndSendAsync<TId, TItem>(TId itemId, IItemCommand<TItem> command, CancellationToken cancelationToken = default)
            where TItem : class, IItem<TId>
        {
            if (itemId == null)
                throw new ArgumentNullException(nameof(itemId));
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var itemProvider = serviceProvider.GetRequiredService<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdASync(itemId, cancelationToken);
            if (item == null)
                return Result.Error(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await SendItemAsync(item, command, cancelationToken);
        }
        public async Task<Result> FindItemAndSendAsync<TId, TItem, TResultData>(TId itemId, IItemCommand<TItem, TResultData> command, CancellationToken cancelationToken = default)
            where TItem : class, IItem<TId>
        {
            if (itemId == null)
                throw new ArgumentNullException(nameof(itemId));
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var itemProvider = serviceProvider.GetRequiredService<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdASync(itemId, cancelationToken);
            if (item == null)
                return Result.Error(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await SendItemAsync<TId, TItem>(item, command, cancelationToken);
        }

        public async Task<Result> SendItemAsync<TId, TItem>(IItem<TId> item, IItemCommand<TItem> command, CancellationToken cancelationToken = default)
            where TItem : class, IItem<TId>
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var commandType = command.GetType();
            if (!options.TryGetHandlerNotResult(commandType, out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");

            using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, scope.ServiceProvider);
            if (!validationResult.IsSuccess)
                return validationResult;

            var handlerObject = CreateCommandHandler(commandMetadata, scope.ServiceProvider);

            var handlerTask = (Task<Result>)commandMetadata.HandleMethod.Invoke(handlerObject, new object[] { item, command, cancelationToken });

            return await handlerTask;
        }
        public async Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(IItem<TId> item, IItemCommand<TItem, TResultData> command, CancellationToken cancelationToken = default)
            where TItem : class, IItem<TId>
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!options.TryGetHandlerWithResult<TResultData>(out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by result \"{typeof(TResultData).AssemblyQualifiedName}\".");

            using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, scope.ServiceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TResultData>();

            var handlerObject = CreateCommandHandler(commandMetadata, scope.ServiceProvider);

            var handlerTask = (Task<Result<TResultData>>)commandMetadata.HandleMethod.Invoke(handlerObject, new object[] { item, command, cancelationToken });

            return await handlerTask;
        }

        #endregion

        private static Result ValidateObj(object obj, IServiceProvider serviceProvider)
        {
            var commandValidator = serviceProvider.GetService<IValidator>();
            if (commandValidator != null)
            {
                var errors = new List<CommandValidationError>();
                if (!commandValidator.Validate(obj, serviceProvider, errors))
                    return Result.Error(errors);
            }
            return Result.Success();
        }
        private static object CreateQueryHandler(QueryMetadata queryMetadata, IServiceProvider serviceProvider)
        {
            var constructorParams = new List<object>();
            foreach (var constructorParamType in queryMetadata.ConstructorParamTypes)
            {
                var paramValue = serviceProvider.GetRequiredService(constructorParamType);
                constructorParams.Add(paramValue);
            }

            return queryMetadata.Constructor.Invoke(constructorParams.ToArray());
        }
        private static object CreateCommandHandler(CommandMetadata commandMetadata, IServiceProvider serviceProvider)
        {
            var constructorParams = new List<object>();
            foreach (var constructorParamType in commandMetadata.ConstructorParamTypes)
            {
                var paramValue = serviceProvider.GetRequiredService(constructorParamType);
                constructorParams.Add(paramValue);
            }

            return commandMetadata.Constructor.Invoke(constructorParams.ToArray());
        }
    }
}