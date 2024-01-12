using System.Collections.Generic;

namespace BrandUp.Decorators
{
    public class DecoratorOptions
    {
        internal List<DecoratorMetadata> DecoratorsMetadata { get; } = [];
        public DecoratorOptions AddDecorator<T>() where T : class, ICommandDecorator
        {
            DecoratorsMetadata.Add(DecoratorMetadata.Build(typeof(T)));
            return this;
        }
    }
}