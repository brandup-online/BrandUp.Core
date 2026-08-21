namespace BrandUp
{
    /// <summary>
    /// Semantic category of an <see cref="IError"/>. Lets transport layers (HTTP, gRPC) map
    /// domain errors to protocol status codes without parsing error codes.
    /// </summary>
    public enum ErrorKind
    {
        /// <summary>A general domain error with no specific category.</summary>
        Unspecified = 0,

        /// <summary>The request is malformed or fails validation rules.</summary>
        Validation,

        /// <summary>The addressed entity does not exist.</summary>
        NotFound,

        /// <summary>The caller is not authenticated.</summary>
        Unauthorized,

        /// <summary>The caller is authenticated but not allowed to perform the operation.</summary>
        Forbidden,

        /// <summary>The operation conflicts with the current state of the entity.</summary>
        Conflict,

        /// <summary>An internal failure not caused by the caller.</summary>
        Internal
    }
}
