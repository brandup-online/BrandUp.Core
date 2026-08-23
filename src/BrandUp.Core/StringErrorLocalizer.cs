using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace BrandUp
{
    /// <summary>
    /// <see cref="IErrorLocalizer"/> backed by an <see cref="IStringLocalizer"/>: the error code
    /// is the resource key, the resource value is a localized message template formatted with
    /// <see cref="IError.Arguments"/> in the target culture. Templates are resolved along the
    /// culture parent chain (<c>ru-RU</c> falls back to <c>ru</c>) but never to the neutral
    /// resources — a code translated only in the default language reports as untranslated
    /// (<see langword="null"/>), so the caller falls back to the invariant message and
    /// completeness checks actually catch missing translations. A malformed translated template
    /// also reports <see langword="null"/> instead of failing the response: translations are
    /// data, not code. Per-culture template sets are cached for the lifetime of the instance.
    /// </summary>
    public sealed class StringErrorLocalizer(IStringLocalizer localizer) : IErrorLocalizer
    {
        // Culture names come from the request (Accept-Language drives CurrentUICulture), and
        // .NET constructs a CultureInfo for any well-formed BCP-47 tag - without a cap a scanner
        // cycling culture names would grow the cache for the process lifetime. Cultures beyond
        // the cap are served uncached.
        const int CultureCacheLimit = 64;

        readonly IStringLocalizer localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        readonly ConcurrentDictionary<string, Dictionary<string, string>> templatesByCulture = new();

        /// <inheritdoc/>
        public string? Localize(IError error, CultureInfo culture)
        {
            ArgumentNullException.ThrowIfNull(error);
            ArgumentNullException.ThrowIfNull(culture);

            if (error.Code.Length == 0)
                return null;

            // Walk the parent chain excluding the invariant culture: the neutral resources are
            // the untranslated default, not a translation.
            for (var candidate = culture; !Equals(candidate, CultureInfo.InvariantCulture); candidate = candidate.Parent)
            {
                if (!GetTemplates(candidate).TryGetValue(error.Code, out var template))
                    continue;

                var arguments = error.Arguments;
                if (arguments.Count == 0)
                    return template;

                try
                {
                    return string.Format(culture, template, arguments as object?[] ?? [.. arguments]);
                }
                catch (FormatException)
                {
                    // A broken translation must not break the response.
                    return null;
                }
            }

            return null;
        }

        Dictionary<string, string> GetTemplates(CultureInfo culture)
        {
            if (templatesByCulture.TryGetValue(culture.Name, out var cached))
                return cached;

            var templates = BuildTemplates(culture);

            if (templatesByCulture.Count < CultureCacheLimit)
                templatesByCulture.TryAdd(culture.Name, templates);

            return templates;
        }

        Dictionary<string, string> BuildTemplates(CultureInfo culture)
        {
            // IStringLocalizer.GetAllStrings(false) enumerates the exact-culture resource
            // set of CultureInfo.CurrentUICulture: the swap happens once per culture per
            // instance, not per lookup.
            var previousCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = culture;
                return localizer.GetAllStrings(includeParentCultures: false)
                    .ToDictionary(localized => localized.Name, localized => localized.Value);
            }
            catch (MissingManifestResourceException)
            {
                // No resource set for this culture at all.
                return [];
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
            }
        }
    }
}
