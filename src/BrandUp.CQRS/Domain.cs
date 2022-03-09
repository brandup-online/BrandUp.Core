using BrandUp.Commands;
using BrandUp.Commands.Validation;
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

        public async Task<IResult> SendAsync(ICommand command, CancellationToken cancelationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var commandType = command.GetType();
            if (!options.TryGetHandlerNotResult(commandType, out CommandMetadata commandMetadata))
                throw new InvalidOperationException($"Not found handler by command \"{commandType.AssemblyQualifiedName}\"");

            using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateCommand(command, scope.ServiceProvider);
            if (!validationResult.IsSuccess)
                return validationResult;

            var handlerObject = CreateHandler(commandMetadata, scope.ServiceProvider);

            var handlerTask = (Task)commandMetadata.HandleMethod.Invoke(handlerObject, new object[] { command, cancelationToken });

            await handlerTask;

            return Result.Success();
        }

        public async Task<IResult<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancelationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!options.TryGetHandlerWithResultResult<TResult>(out CommandMetadata commandMetadata))
                throw new InvalidOperationException();

            using var scope = serviceProvider.CreateScope();

            var validationResult = ValidateCommand(command, scope.ServiceProvider);
            if (!validationResult.IsSuccess)
                return validationResult.AsObjectiveErrors<TResult>();

            var handlerObject = CreateHandler(commandMetadata, scope.ServiceProvider);

            var handlerTask = (Task<TResult>)commandMetadata.HandleMethod.Invoke(handlerObject, new object[] { command, cancelationToken });

            var result = await handlerTask;

            return Result.Success(result);
        }

        #endregion

        private static IResult ValidateCommand(ICommand command, IServiceProvider serviceProvider)
        {
            var commandValidator = serviceProvider.GetService<ICommandValidator>();
            if (commandValidator != null)
            {
                var errors = new List<CommandValidationError>();
                if (!commandValidator.Validate(command, serviceProvider, errors))
                    return Result.Error(errors);
            }
            return Result.Success();
        }

        private static object CreateHandler(CommandMetadata commandMetadata, IServiceProvider serviceProvider)
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