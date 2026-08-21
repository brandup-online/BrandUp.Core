using System.Globalization;

namespace BrandUp
{
    /// <summary>
    /// A cataloged domain error: a stable code (the API contract), its semantic category, an
    /// invariant developer-facing message template and an optional description for integrator
    /// documentation. Declare descriptors as <see langword="static"/> members of per-domain
    /// catalog classes and register them in an <see cref="ErrorCatalog"/>; raise with
    /// <see cref="Result.Error(ErrorDescriptor, object?[])"/>. Localized messages are produced at
    /// the transport layer by an <see cref="IErrorLocalizer"/> from the code and the arguments.
    /// </summary>
    public sealed class ErrorDescriptor
    {
        /// <summary>
        /// Stable machine-readable error code (e.g. <c>order-not-found</c>).
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Semantic category of the error.
        /// </summary>
        public ErrorKind Kind { get; }

        /// <summary>
        /// Invariant message template in <see cref="string.Format(IFormatProvider, string, object?[])"/>
        /// syntax (e.g. <c>Order {0} not found.</c>). Developer-facing; localized templates live in
        /// the transport layer's resources under <see cref="Code"/>.
        /// </summary>
        public string MessageTemplate { get; }

        /// <summary>
        /// When and why the error occurs — surfaced by catalog documentation endpoints.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Creates a descriptor.
        /// </summary>
        /// <param name="code">Stable machine-readable error code; required.</param>
        /// <param name="kind">Semantic category of the error.</param>
        /// <param name="messageTemplate">Invariant message template; required.</param>
        /// <param name="description">Optional documentation description.</param>
        /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="messageTemplate"/> is null or empty.</exception>
        public ErrorDescriptor(string code, ErrorKind kind, string messageTemplate, string? description = null)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Error code is required.", nameof(code));
            if (string.IsNullOrEmpty(messageTemplate))
                throw new ArgumentException("Error message template is required.", nameof(messageTemplate));

            Code = code;
            Kind = kind;
            MessageTemplate = messageTemplate;
            Description = description;
        }

        /// <summary>
        /// Builds an <see cref="Error"/> from this descriptor: the invariant message is formatted
        /// from the template and the arguments are preserved for transport-layer localization.
        /// </summary>
        /// <param name="arguments">Format arguments of the message template. A single array
        /// argument binds as the whole params array — cast it to <see cref="object"/> to pass an
        /// array as one argument.</param>
        public Error CreateError(params object?[] arguments)
        {
            arguments ??= [];

            var message = arguments.Length > 0
                ? string.Format(CultureInfo.InvariantCulture, MessageTemplate, arguments)
                : MessageTemplate;

            return new Error(Code, message, Kind, arguments);
        }
    }
}
