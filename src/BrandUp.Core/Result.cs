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
        public int CountErrors => errors?.Length ?? 0;

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
        /// Creates a failed typed result from a single error.
        /// </summary>
        /// <typeparam name="TData">Type of the data the result would carry on success.</typeparam>
        /// <param name="code">Error code.</param>
        /// <param name="message">Error message.</param>
        public static Result<TData> Error<TData>(string code, string message)
        {
            return new Result<TData>([new Error(code, message)]);
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
                return $"Errors: {CountErrors}";
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
                return $"Errors ({dataType.FullName}): {CountErrors}";
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

        /// <summary>
        /// Creates an error.
        /// </summary>
        /// <param name="code">Error code; <see langword="null"/> is stored as an empty string.</param>
        /// <param name="message">Error message; required.</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> is null or empty.</exception>
        public Error(string? code, string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Error message is required.");

            Code = code ?? string.Empty;
            Message = message;
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
    }
}