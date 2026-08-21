using System.Diagnostics.CodeAnalysis;

namespace BrandUp.Testing
{
    /// <summary>
    /// Failure-reporting seam of the testing helpers. By default failures throw
    /// <see cref="DomainAssertException"/>, which every test framework reports as a failed test;
    /// a framework adapter (e.g. BrandUp.Core.Testing.xUnit) replaces the exception factory so
    /// failures surface as native assertion failures of that framework.
    /// </summary>
    public static class DomainAssert
    {
        static Func<string, Exception> exceptionFactory = static message => new DomainAssertException(message);

        /// <summary>
        /// Replaces the exception used to report assertion failures. Called once by framework
        /// adapters; safe to call repeatedly.
        /// </summary>
        /// <param name="factory">Builds the exception for a failure message.</param>
        public static void UseExceptionFactory(Func<string, Exception> factory)
        {
            exceptionFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Builds the failure exception for the message — use as <c>throw DomainAssert.Failure(...)</c>.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public static Exception Failure(string message)
        {
            return exceptionFactory(message);
        }

        /// <summary>
        /// Reports an assertion failure by throwing the configured exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        [DoesNotReturn]
        public static void Fail(string message)
        {
            throw exceptionFactory(message);
        }
    }
}
