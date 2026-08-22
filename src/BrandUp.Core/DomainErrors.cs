namespace BrandUp
{
    /// <summary>
    /// Descriptors of the errors the library itself raises, following the same catalog-class
    /// pattern application domains use. Register them in the application's
    /// <see cref="ErrorCatalog"/> (<c>catalog.AddFrom(typeof(DomainErrors))</c>) to document and
    /// localize the built-in errors alongside the domain's own.
    /// </summary>
    public static class DomainErrors
    {
        /// <summary>
        /// The target item of an id-based command does not exist
        /// (see <see cref="DomainExtensions"/>).
        /// </summary>
        public static readonly ErrorDescriptor ItemNotFound = new(
            "item-not-found", ErrorKind.NotFound,
            "Item \"{0}\" not found.",
            "The target item of an id-based command does not exist.");

        /// <summary>
        /// An <see cref="Authorization.IDomainAuthorizer{TRequest}"/> denied the dispatch
        /// (the default denial; authorizers may return their own cataloged errors instead).
        /// </summary>
        public static readonly ErrorDescriptor AccessDenied = new(
            "access-denied", ErrorKind.Forbidden,
            "Access to the requested operation is denied.",
            "The caller is authenticated but is not allowed to execute the operation.");

        /// <summary>
        /// The dispatch exceeded the timeout declared by the request
        /// (see <see cref="Behaviors.IDispatchTimeout"/>).
        /// </summary>
        public static readonly ErrorDescriptor DispatchTimeout = new(
            "dispatch-timeout", ErrorKind.Internal,
            "The operation \"{0}\" timed out.",
            "The dispatch exceeded the timeout declared by the request and was cancelled.");

        /// <summary>
        /// An idempotent command with the same idempotency key is already being processed
        /// (see <see cref="Idempotency.IIdempotentCommand"/>).
        /// </summary>
        public static readonly ErrorDescriptor DuplicateRequest = new(
            "duplicate-request", ErrorKind.Conflict,
            "The request \"{0}\" is already being processed.",
            "A command with the same idempotency key is currently in flight; retry after it completes.");
    }
}
