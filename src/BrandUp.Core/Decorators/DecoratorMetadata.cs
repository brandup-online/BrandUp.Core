using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace BrandUp.Decorators
{
    internal class DecoratorMetadata
    {
        readonly ConstructorInfo constructor;
        readonly IReadOnlyCollection<Type> constructorParamTypes;

        public ConstructorInfo Constructor => constructor;
        public IReadOnlyCollection<Type> ConstructorParamTypes => constructorParamTypes;

        internal static DecoratorMetadata Build(Type decoratorType)
        {
            var constructors = decoratorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if (constructors.Length > 1)
                throw new InvalidOperationException();
            else if (constructors.Length == 0)
                throw new InvalidOperationException();
            var constructor = constructors[0];

            var constructorParams = constructor.GetParameters();
            var constructorParamTypes = new ReadOnlyCollection<Type>(constructorParams.Select(it => it.ParameterType).ToList());

            return new DecoratorMetadata(constructor, constructorParamTypes);
        }

        private DecoratorMetadata(ConstructorInfo constructor, IReadOnlyCollection<Type> constructorParamTypes)
        {
            this.constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
            this.constructorParamTypes = constructorParamTypes ?? throw new ArgumentNullException(nameof(constructorParamTypes));
        }
    }
}
