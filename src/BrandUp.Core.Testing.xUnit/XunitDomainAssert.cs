using System.Runtime.CompilerServices;
using Xunit.Sdk;

namespace BrandUp.Testing.Xunit
{
    /// <summary>
    /// A <see cref="DomainAssertException"/> that xUnit recognizes as an assertion failure
    /// (<see cref="IAssertionException"/>): the runner reports it as a failed assertion with a
    /// trimmed stack trace instead of a generic unhandled exception.
    /// </summary>
    public class XunitDomainAssertException(string message) : DomainAssertException(message), IAssertionException
    {
    }

    /// <summary>
    /// Wires <see cref="DomainAssert"/> to xUnit. The module initializer applies the wiring as
    /// soon as this assembly loads; calling <see cref="Use"/> explicitly (e.g. from a module
    /// initializer of the test project) guarantees the load and is safe to repeat.
    /// </summary>
    public static class XunitDomainAssert
    {
        /// <summary>
        /// Makes domain assertion failures throw <see cref="XunitDomainAssertException"/>.
        /// </summary>
        public static void Use()
        {
            DomainAssert.UseExceptionFactory(static message => new XunitDomainAssertException(message));
        }

        // Deliberate library use of a module initializer: this assembly is only ever loaded into
        // test processes, and the initializer is what makes referencing the adapter sufficient.
#pragma warning disable CA2255
        [ModuleInitializer]
        internal static void Initialize()
        {
            Use();
        }
#pragma warning restore CA2255
    }
}
