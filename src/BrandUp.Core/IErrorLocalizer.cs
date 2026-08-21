using System.Globalization;

namespace BrandUp
{
    /// <summary>
    /// Resolves a localized message for a domain error at the transport layer. The domain itself
    /// carries only the stable <see cref="IError.Code"/>, the invariant developer-facing
    /// <see cref="IError.Message"/> and the <see cref="IError.Arguments"/>; localization happens
    /// where the culture of the consumer is known — per HTTP request, not at error creation —
    /// so cached results, stored outbox events and logs stay culture-free.
    /// </summary>
    public interface IErrorLocalizer
    {
        /// <summary>
        /// Returns the localized message for the error in the given culture, or
        /// <see langword="null"/> when no translation exists — the caller then falls back to
        /// <see cref="IError.Message"/>.
        /// </summary>
        /// <param name="error">Error to localize (the code is the resource key).</param>
        /// <param name="culture">Target culture.</param>
        string? Localize(IError error, CultureInfo culture);
    }
}
