namespace BrandUp.Testing
{
    /// <summary>
    /// Thrown by the BrandUp.Core.Testing assertions. Any test framework reports an unhandled
    /// exception as a test failure, so the helpers work with xUnit, NUnit and MSTest alike
    /// without depending on any of them.
    /// </summary>
    public class DomainAssertException(string message) : Exception(message)
    {
    }
}
