using System.Globalization;

namespace BrandUp
{
    /// <summary>
    /// Extensions of <see cref="IError"/>.
    /// </summary>
    public static class ErrorExtensions
    {
        /// <summary>
        /// The one localize-with-fallback rule for error messages: the localized message when the
        /// localizer has a translation for the code, the invariant <see cref="IError.Message"/>
        /// otherwise. Public so transports keeping their own response format resolve messages
        /// exactly the way the built-in HTTP mapping does. A blank translation counts as no
        /// translation — a resource row left empty must not become an empty error message.
        /// </summary>
        /// <param name="error">Error whose message to resolve.</param>
        /// <param name="errorLocalizer">Localizer resolving messages by error code; <see langword="null"/> keeps the invariant message.</param>
        /// <param name="culture">Target culture; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        public static string LocalizeOrInvariant(this IError error, IErrorLocalizer? errorLocalizer, CultureInfo? culture = null)
        {
            ArgumentNullException.ThrowIfNull(error);

            var localized = errorLocalizer?.Localize(error, culture ?? CultureInfo.CurrentUICulture);

            return string.IsNullOrEmpty(localized) ? error.Message : localized;
        }
    }
}
