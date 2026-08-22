namespace BrandUp.Behaviors
{
    /// <summary>
    /// Declares an upper bound on how long the dispatch of this query or command may run.
    /// Requires <see cref="TimeoutBehavior"/> (see
    /// <see cref="DomainBuilderExtensions.AddDispatchTimeouts"/>); on expiry the dispatch is
    /// cancelled and returns the <see cref="DomainErrors.DispatchTimeout"/> error instead of
    /// throwing.
    /// </summary>
    public interface IDispatchTimeout
    {
        /// <summary>
        /// Maximum duration of the dispatch; a non-positive value disables the timeout.
        /// </summary>
        TimeSpan Timeout { get; }
    }
}
