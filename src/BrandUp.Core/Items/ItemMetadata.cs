using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace BrandUp.Items
{
    public class ItemMetadata
    {
        private ConstructorInfo constructor;
        private IReadOnlyCollection<Type> constructorParamTypes;

        public Type ProviderType { get; private set; }
        public Type IdType { get; private set; }
        public Type ItemType { get; private set; }
        public ConstructorInfo Constructor => constructor;
        public IReadOnlyCollection<Type> ConstructorParamTypes => constructorParamTypes;

        public static ItemMetadata Build(Type providerType, Type idType, Type itemType)
        {
            var itemMetadata = new ItemMetadata
            {
                ProviderType = providerType,
                IdType = idType,
                ItemType = itemType
            };

            var constructors = providerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if (constructors.Length > 1)
                throw new InvalidOperationException($"Multiply constructors for type \"{providerType.AssemblyQualifiedName}\".");
            else if (constructors.Length == 0)
                throw new InvalidOperationException($"Not found constructor for type \"{providerType.AssemblyQualifiedName}\".");

            itemMetadata.constructor = constructors[0];
            var constructorParams = itemMetadata.constructor.GetParameters();
            itemMetadata.constructorParamTypes = new ReadOnlyCollection<Type>(constructorParams.Select(it => it.ParameterType).ToList());

            return itemMetadata;
        }
    }
}