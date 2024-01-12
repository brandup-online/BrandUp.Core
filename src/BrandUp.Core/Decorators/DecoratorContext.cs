using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BrandUp.Decorators
{
    public class DecoratorContext(IServiceProvider serviceProvider, IOptions<DecoratorOptions> options)
    {
        readonly IServiceProvider serviceProvider = serviceProvider;
        readonly List<DecoratorMetadata> commandDecoratorsMetadata = options?.Value.DecoratorsMetadata ?? [];
        readonly List<ICommandDecorator> commandDecorators = [];

        internal List<ICommandDecorator> CommandDecorators
        {
            get
            {
                if (commandDecorators.Count == 0)
                {
                    return CreateDecorators();
                }
                return commandDecorators;
            }
        }

        private List<ICommandDecorator> CreateDecorators()
        {
            var decorators = new List<ICommandDecorator>();
            foreach (var decoratorMetadata in commandDecoratorsMetadata)
            {
                var constructorParams = new List<object>();
                foreach (var constructorParamType in decoratorMetadata.ConstructorParamTypes)
                {
                    var paramValue = serviceProvider.GetRequiredService(constructorParamType);
                    constructorParams.Add(paramValue);
                }
                decorators.Add((ICommandDecorator)decoratorMetadata.Constructor.Invoke([.. constructorParams]));
            }

            return decorators;
        }
    }
}
