using System.Text;

namespace BrandUp.Testing
{
    /// <summary>
    /// Assertions over <see cref="Result"/>/<see cref="Result{TData}"/>. Failure messages list
    /// every error with its code and kind, not just the first one.
    /// </summary>
    public static class ResultAssertExtensions
    {
        /// <summary>
        /// Asserts the result is successful.
        /// </summary>
        /// <param name="result">Result to check.</param>
        /// <returns>The same result, for chaining.</returns>
        /// <exception cref="DomainAssertException">The result is failed.</exception>
        public static Result AssertSuccess(this Result result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (!result.IsSuccess)
                throw DomainAssert.Failure($"Expected {result.GetType().Name} to succeed, but it failed: {ErrorText(result)}.");

            return result;
        }

        /// <summary>
        /// Asserts the result is successful, optionally checks its data, and returns the data.
        /// </summary>
        /// <typeparam name="TData">Type of the carried data.</typeparam>
        /// <param name="result">Result to check.</param>
        /// <param name="check">Optional additional check over the data.</param>
        /// <returns>The result data.</returns>
        /// <exception cref="DomainAssertException">The result is failed.</exception>
        public static TData AssertSuccess<TData>(this Result<TData> result, Action<TData>? check = null)
        {
            ArgumentNullException.ThrowIfNull(result);

            ((Result)result).AssertSuccess();

            check?.Invoke(result.Data);

            return result.Data;
        }

        /// <summary>
        /// Asserts the result is failed, optionally that some error matches the given code and
        /// kind, and returns the matched error.
        /// </summary>
        /// <param name="result">Result to check.</param>
        /// <param name="code">Expected error code of at least one error; <see langword="null"/> to skip.</param>
        /// <param name="kind">Expected error kind of at least one error; <see langword="null"/> to skip.</param>
        /// <returns>The first error matching the filters (or the first error when no filters are given).</returns>
        /// <exception cref="DomainAssertException">The result is successful, or no error matches the filters.</exception>
        public static IError AssertError(this Result result, string? code = null, ErrorKind? kind = null)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsSuccess)
                throw DomainAssert.Failure($"Expected {result.GetType().Name} to fail, but it succeeded.");

            foreach (var error in result.Errors)
            {
                if (code != null && error.Code != code)
                    continue;
                if (kind.HasValue && error.Kind != kind.Value)
                    continue;

                return error;
            }

            throw DomainAssert.Failure($"Expected {result.GetType().Name} to fail with {Expectation(code, kind)}, but its errors are: {ErrorText(result)}.");
        }

        /// <summary>
        /// Asserts the result is failed with the cataloged error: some error matches the
        /// descriptor's code and kind. Refactoring-safe alternative to string codes.
        /// </summary>
        /// <param name="result">Result to check.</param>
        /// <param name="descriptor">Expected error descriptor.</param>
        /// <returns>The matched error.</returns>
        /// <exception cref="DomainAssertException">The result is successful, or no error matches.</exception>
        public static IError AssertError(this Result result, ErrorDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            return result.AssertError(descriptor.Code, descriptor.Kind);
        }

        internal static string ErrorText(Result result)
        {
            var text = new StringBuilder();
            foreach (var error in result.Errors)
            {
                if (text.Length > 0)
                    text.Append("; ");

                text.Append('[');
                text.Append(error.Code);
                if (error.Kind != ErrorKind.Unspecified)
                    text.Append('/').Append(error.Kind);
                text.Append("] ");
                text.Append(error.Message);
            }

            return text.ToString();
        }

        static string Expectation(string? code, ErrorKind? kind)
        {
            if (code != null && kind.HasValue)
                return $"code \"{code}\" and kind {kind}";
            if (code != null)
                return $"code \"{code}\"";
            return $"kind {kind}";
        }
    }
}
