using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace BrandUp
{
    /// <summary>
    /// Registry of every <see cref="ErrorDescriptor"/> the domain can raise: the single source for
    /// documentation endpoints, localization completeness checks and code-uniqueness validation.
    /// Populate via <see cref="DomainBuilderExtensions.AddErrorCatalog"/>; registered as a
    /// singleton.
    /// </summary>
    public sealed class ErrorCatalog
    {
        readonly Dictionary<string, ErrorDescriptor> descriptors = [];

        /// <summary>
        /// Every registered descriptor.
        /// </summary>
        public IReadOnlyCollection<ErrorDescriptor> All => descriptors.Values;

        /// <summary>
        /// Registers a descriptor.
        /// </summary>
        /// <param name="descriptor">Descriptor to register.</param>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="InvalidOperationException">A descriptor with the same code is already registered.</exception>
        public ErrorCatalog Add(ErrorDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            if (!descriptors.TryAdd(descriptor.Code, descriptor))
                throw new InvalidOperationException($"Error descriptor with code \"{descriptor.Code}\" is already registered.");

            return this;
        }

        /// <summary>
        /// Registers every descriptor declared as a public static field of the type
        /// (the per-domain catalog class pattern).
        /// </summary>
        /// <param name="declaringType">Type declaring the descriptors.</param>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The type declares no descriptors, or a code is duplicated.</exception>
        public ErrorCatalog AddFrom(Type declaringType)
        {
            ArgumentNullException.ThrowIfNull(declaringType);

            if (AddFromMembers(declaringType) == 0)
                throw new InvalidOperationException($"Type \"{declaringType.AssemblyQualifiedName}\" declares no public static {nameof(ErrorDescriptor)} fields.");

            return this;
        }

        /// <summary>
        /// Scans the assembly and registers every descriptor declared as a public static field
        /// of any type.
        /// </summary>
        /// <param name="assembly">Assembly to scan.</param>
        /// <param name="typeFilter">Optional filter; a type it rejects is skipped.</param>
        /// <returns>This instance, for chaining.</returns>
        public ErrorCatalog AddFromAssembly(Assembly assembly, Func<Type, bool>? typeFilter = null)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (typeFilter != null && !typeFilter(type))
                    continue;

                AddFromMembers(type);
            }

            return this;
        }

        /// <summary>
        /// Looks up a descriptor by its code.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="descriptor">The found descriptor; never <see langword="null"/> when the
        /// method returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> if the code is registered.</returns>
        public bool TryGet(string code, [MaybeNullWhen(false)] out ErrorDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(code);

            return descriptors.TryGetValue(code, out descriptor);
        }

        // Fields only: scanning properties would invoke arbitrary user getters during an
        // assembly scan, and the documented catalog-class pattern uses static readonly fields.
        int AddFromMembers(Type declaringType)
        {
            var found = 0;

            foreach (var field in declaringType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(ErrorDescriptor) && field.GetValue(null) is ErrorDescriptor descriptor)
                {
                    Add(descriptor);
                    found++;
                }
            }

            return found;
        }
    }
}
