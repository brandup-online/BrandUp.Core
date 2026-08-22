namespace BrandUp
{
    /// <summary>
    /// Outcome of an operation: either success or a non-empty set of errors.
    /// </summary>
    public class Result
    {
        readonly IError[]? errors;

        /// <summary>
        /// <see langword="true"/> when the operation succeeded (no errors).
        /// </summary>
        public bool IsSuccess => errors == null;

        /// <summary>
        /// The errors of a failed result, or an empty sequence on success.
        /// </summary>
        public IEnumerable<IError> Errors => errors ?? [];

        /// <summary>
        /// Number of errors; <c>0</c> on success.
        /// </summary>
        public int ErrorCount => errors?.Length ?? 0;

        internal Result() { }
        internal Result(IList<IError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (errors.Count == 0)
                throw new ArgumentException("Errors required.", nameof(errors));

            this.errors = [.. errors];
        }

        #region Success

        /// <summary>
        /// Creates a successful result without data.
        /// </summary>
        public static Result Success()
        {
            return new Result();
        }

        /// <summary>
        /// Creates a successful result carrying <paramref name="obj"/> as data.
        /// </summary>
        /// <typeparam name="TData">Type of the data.</typeparam>
        /// <param name="obj">Data to carry.</param>
        public static Result<TData> Success<TData>(TData obj)
        {
            return new Result<TData>(obj);
        }

        #endregion

        #region Error

        /// <summary>
        /// Creates a failed result from the given errors.
        /// </summary>
        /// <param name="errors">Non-empty set of errors.</param>
        /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
        public static Result Error(IEnumerable<IError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (!errors.Any())
                throw new ArgumentException("Errors is empty", nameof(errors));

            return new Result(new List<IError>(errors));
        }

        /// <summary>
        /// Creates a failed typed result from the given errors.
        /// </summary>
        /// <typeparam name="TData">Type of the data the result would carry on success.</typeparam>
        /// <param name="errors">Non-empty set of errors.</param>
        /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
        public static Result<TData> Error<TData>(IEnumerable<IError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (!errors.Any())
                throw new ArgumentException("Errors is empty", nameof(errors));

            return new Result<TData>(new List<IError>(errors));
        }

        /// <summary>
        /// Creates a failed result from a single error.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        public static Result Error(string code, string message)
        {
            return new Result([new Error(code, message)]);
        }

        /// <summary>
        /// Creates a failed result from a single categorized error.
        /// </summary>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        /// <param name="kind">Semantic category of the error.</param>
        public static Result Error(string code, string message, ErrorKind kind)
        {
            return new Result([new Error(code, message, kind)]);
        }

        /// <summary>
        /// Creates a failed typed result from a single error.
        /// </summary>
        /// <typeparam name="TData">Type of the data the result would carry on success.</typeparam>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        public static Result<TData> Error<TData>(string code, string message)
        {
            return new Result<TData>([new Error(code, message)]);
        }

        /// <summary>
        /// Creates a failed typed result from a single categorized error.
        /// </summary>
        /// <typeparam name="TData">Type of the data the result would carry on success.</typeparam>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        /// <param name="kind">Semantic category of the error.</param>
        public static Result<TData> Error<TData>(string code, string message, ErrorKind kind)
        {
            return new Result<TData>([new Error(code, message, kind)]);
        }

        /// <summary>
        /// Creates a failed result from a cataloged error descriptor: the code and kind come from
        /// the descriptor, the message from its invariant template and the arguments.
        /// </summary>
        /// <param name="descriptor">Descriptor of the error (see <see cref="ErrorCatalog"/>).</param>
        /// <param name="arguments">Format arguments of the message template. A single array
        /// argument binds as the whole params array — cast it to <see cref="object"/> to pass an
        /// array as one argument.</param>
        public static Result Error(ErrorDescriptor descriptor, params object?[] arguments)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            return new Result([descriptor.CreateError(arguments)]);
        }

        /// <summary>
        /// Creates a failed typed result from a cataloged error descriptor.
        /// </summary>
        /// <typeparam name="TData">Type of the data the result would carry on success.</typeparam>
        /// <param name="descriptor">Descriptor of the error (see <see cref="ErrorCatalog"/>).</param>
        /// <param name="arguments">Format arguments of the message template. A single array
        /// argument binds as the whole params array — cast it to <see cref="object"/> to pass an
        /// array as one argument.</param>
        public static Result<TData> Error<TData>(ErrorDescriptor descriptor, params object?[] arguments)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            return new Result<TData>([descriptor.CreateError(arguments)]);
        }

        #endregion

        /// <summary>
        /// Implicitly converts a result to a <see cref="bool"/> equal to <see cref="IsSuccess"/>.
        /// </summary>
        /// <param name="d">Result to convert.</param>
        public static implicit operator bool(Result d) => d.IsSuccess;

        #region Object members

        /// <inheritdoc/>
        public override string ToString()
        {
            if (IsSuccess)
                return "Success";
            else
                return $"Errors: {ErrorCount}";
        }

        #endregion
    }

    /// <summary>
    /// Outcome of an operation that carries data of type <typeparamref name="TData"/> on success.
    /// </summary>
    /// <typeparam name="TData">Type of the carried data.</typeparam>
    public class Result<TData> : Result
    {
        /// <summary>
        /// Data of a successful result; the default value of <typeparamref name="TData"/> on failure.
        /// </summary>
        public TData Data { get; } = default!;

        internal Result(TData data)
        {
            Data = data;
        }

        internal Result(IList<IError> errors) : base(errors) { }

        #region Object members

        /// <inheritdoc/>
        public override string ToString()
        {
            var dataType = typeof(TData);

            if (IsSuccess)
                return $"Success ({dataType.FullName})";
            else
                return $"Errors ({dataType.FullName}): {ErrorCount}";
        }

        #endregion
    }

    /// <summary>
    /// Default <see cref="IError"/> implementation with a code and a message.
    /// </summary>
    public class Error : IError
    {
        /// <inheritdoc/>
        public string Code { get; }

        /// <inheritdoc/>
        public string Message { get; }

        /// <inheritdoc/>
        public ErrorKind Kind { get; }

        /// <inheritdoc/>
        public IReadOnlyList<object?> Arguments { get; } = [];

        /// <summary>
        /// Creates an error with <see cref="ErrorKind.Unspecified"/>. Kept as a distinct
        /// constructor (not an optional parameter) for binary compatibility with assemblies
        /// compiled against earlier versions.
        /// </summary>
        /// <param name="code">Error code; <see langword="null"/> is stored as an empty string.</param>
        /// <param name="message">Error message; required.</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> is null or empty.</exception>
        public Error(string? code, string message)
            : this(code, message, ErrorKind.Unspecified)
        {
        }

        /// <summary>
        /// Creates a categorized error.
        /// </summary>
        /// <param name="code">Error code; <see langword="null"/> is stored as an empty string.</param>
        /// <param name="message">Error message; required.</param>
        /// <param name="kind">Semantic category of the error.</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> is null or empty.</exception>
        public Error(string? code, string message, ErrorKind kind)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Error message is required.");

            Code = code ?? string.Empty;
            Message = message;
            Kind = kind;
        }

        /// <summary>
        /// Creates a categorized error carrying the format arguments its message was built from.
        /// </summary>
        /// <param name="code">Error code; <see langword="null"/> is stored as an empty string.</param>
        /// <param name="message">Error message; required.</param>
        /// <param name="kind">Semantic category of the error.</param>
        /// <param name="arguments">Format arguments of the message; required (may be empty).</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> is null or empty.</exception>
        public Error(string? code, string message, ErrorKind kind, IReadOnlyList<object?> arguments)
            : this(code, message, kind)
        {
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }
    }

    /// <summary>
    /// A single error with a code and a human-readable message.
    /// </summary>
    public interface IError
    {
        /// <summary>
        /// Machine-readable error code (may be empty).
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Semantic category of the error; <see cref="ErrorKind.Unspecified"/> unless the
        /// implementation provides one.
        /// </summary>
        ErrorKind Kind => ErrorKind.Unspecified;

        /// <summary>
        /// Format arguments the <see cref="Message"/> was built from. Transport layers use them
        /// with an <see cref="IErrorLocalizer"/> to rebuild the message from a localized template;
        /// empty when the error carries no arguments.
        /// </summary>
        IReadOnlyList<object?> Arguments => [];
    }
}