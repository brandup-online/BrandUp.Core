using BrandUp.Commands;
using BrandUp.Items;
using BrandUp.Queries;
using BrandUp.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BrandUp
{
    public class Domain(IOptions<DomainOptions> options, IServiceProvider serviceProvider) : IDomain
    {
        readonly DomainOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        #region IDomain members

        public TItemProvider GetItemProvider<TItemProvider>()
        {
            return serviceProvider.GetRequiredService<TItemProvider>();
        }

        public Task<TItem> FindItemAsync<TId, TItem>(TId itemId, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            var itemProvider = serviceProvider.GetRequiredService<IItemProvider<TId, TItem>>();
            return itemProvider.FindByIdAsync(itemId, cancellationToken);
        }

        public async Task<Result<IList<TRow>>> QueryAsync<TRow>(IQuery<TRow> query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var queryType = query.GetType();
            if (!options.TryGetQueryHandler(queryType, out QueryMetadata queryMetadata))
                throw new InvalidOperationException($"Not found query handler by type \"{queryType.AssemblyQualifiedName}\"");

            //using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(query, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<IList<TRow>>();

            var handlerObject = CreateQueryHandler(queryMetadata, serviceProvider);

            var handlerTask = (Task<IList<TRow>>)queryMetadata.HandleMethod.Invoke(handlerObject, [query, cancellationToken]);

            var rows = await handlerTask;

            return Result.Success(rows);
        }

        public async Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetHandlerNotResult(commandType, out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");

            //using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult;

            var handlerObject = CreateCommandHandler(commandMetadata, serviceProvider);

            var handlerTask = (Task<Result>)commandMetadata.HandleMethod.Invoke(handlerObject, [command, cancellationToken]);

            return await handlerTask;
        }

        public async Task<Result<TResultData>> SendAsync<TResultData>(ICommand<TResultData> command, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!options.TryGetHandlerWithResult<TResultData>(out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by result \"{typeof(TResultData).AssemblyQualifiedName}\".");

            //using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TResultData>();

            var handlerObject = CreateCommandHandler(commandMetadata, serviceProvider);

            var handlerTask = (Task<Result<TResultData>>)commandMetadata.HandleMethod.Invoke(handlerObject, [command, cancellationToken]);

            return await handlerTask;
        }

        public async Task<Result> SendItemAsync<TId, TItem>(TId itemId, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(itemId);
            ArgumentNullException.ThrowIfNull(command);

            var itemProvider = serviceProvider.GetRequiredService<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdAsync(itemId, cancellationToken);
            if (item == null)
                return Result.Error(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await SendItemAsync(item, command, cancellationToken);
        }

        public async Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(TId itemId, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(itemId);
            ArgumentNullException.ThrowIfNull(command);

            var itemProvider = serviceProvider.GetRequiredService<IItemProvider<TId, TItem>>();

            var item = await itemProvider.FindByIdAsync(itemId, cancellationToken);
            if (item == null)
                return Result.Error<TResultData>(string.Empty, $"Not found item by ID \"{itemId}\".");

            return await SendItemAsync(item, command, cancellationToken);
        }

        public async Task<Result> SendItemAsync<TId, TItem>(IItem<TId> item, IItemCommand<TItem> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(command);

            var commandType = command.GetType();
            if (!options.TryGetHandlerNotResult(commandType, out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\".");

            //using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult;

            var handlerObject = CreateCommandHandler(commandMetadata, serviceProvider);

            var handlerTask = (Task<Result>)commandMetadata.HandleMethod.Invoke(handlerObject, [item, command, cancellationToken]);

            return await handlerTask;
        }

        public async Task<Result<TResultData>> SendItemAsync<TId, TItem, TResultData>(IItem<TId> item, IItemCommand<TItem, TResultData> command, CancellationToken cancellationToken = default)
            where TItem : class, IItem<TId>
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(command);

            if (!options.TryGetHandlerWithResult<TResultData>(out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by result \"{typeof(TResultData).AssemblyQualifiedName}\".");

            //using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateObj(command, serviceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TResultData>();

            var handlerObject = CreateCommandHandler(commandMetadata, serviceProvider);

            var handlerTask = (Task<Result<TResultData>>)commandMetadata.HandleMethod.Invoke(handlerObject, [item, command, cancellationToken]);

            return await handlerTask;
        }

        #endregion

        static Result ValidateObj(object obj, IServiceProvider serviceProvider)
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

        static object CreateQueryHandler(QueryMetadata queryMetadata, IServiceProvider serviceProvider)
        {
            var constructorParams = new List<object>();
            foreach (var constructorParamType in queryMetadata.ConstructorParamTypes)
            {
                var paramValue = serviceProvider.GetRequiredService(constructorParamType);
                constructorParams.Add(paramValue);
            }

            return queryMetadata.Constructor.Invoke([.. constructorParams]);
        }

        static object CreateCommandHandler(CommandMetadata commandMetadata, IServiceProvider serviceProvider)
        {
            var constructorParams = new List<object>();
            foreach (var constructorParamType in commandMetadata.ConstructorParamTypes)
            {
                var paramValue = serviceProvider.GetRequiredService(constructorParamType);
                constructorParams.Add(paramValue);
            }

            return commandMetadata.Constructor.Invoke([.. constructorParams]);
        }
    }
}