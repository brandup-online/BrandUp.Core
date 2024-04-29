using System.Text;
using Microsoft.Extensions.Logging;

namespace BrandUp
{
    public static class ILoggerExtensions
    {
        /// <summary>
        /// Write log if result with errors.
        /// </summary>
        /// <param name="result">Result object.</param>
        /// <returns>True - has errors. False - <see cref="result"> is success.</returns>
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