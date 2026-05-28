using System.Text;
using Microsoft.Extensions.Logging;

namespace BrandUp
{
    /// <summary>
    /// <see cref="ILogger"/> extensions for logging failed results.
    /// </summary>
    public static class ILoggerExtensions
    {
        /// <summary>
        /// Logs the errors of a failed result, if any.
        /// </summary>
        /// <param name="logger">Logger to write to.</param>
        /// <param name="result">Result to inspect.</param>
        /// <returns><see langword="true"/> if <paramref name="result"/> had errors; otherwise <see langword="false"/>.</returns>
        public static bool LogIfError(this ILogger logger, Result result)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsSuccess)
                return false;

            var errorText = new StringBuilder();
            foreach (var error in result.Errors)
            {
                if (string.IsNullOrEmpty(error.Code))
                    errorText.AppendLine(error.Message);
                else
                    errorText.AppendLine($"{error.Code}: {error.Message}");
            }
            logger.LogError(errorText.ToString().Trim());

            return true;
        }
    }
}