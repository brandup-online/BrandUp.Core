using System.Globalization;

namespace BrandUp.Testing
{
    /// <summary>
    /// Assertions over the <see cref="ErrorCatalog"/> — most importantly localization
    /// completeness, turning "forgot to translate an error code" into a failing test instead of
    /// an English message in production.
    /// </summary>
    public static class ErrorCatalogAssert
    {
        /// <summary>
        /// Asserts every cataloged error code has a working translation in every given culture:
        /// a missing resource and a malformed template (one that fails to format with the
        /// descriptor's placeholder count) both fail. The failure message lists each broken
        /// (culture, code) pair.
        /// </summary>
        /// <param name="catalog">Catalog to check.</param>
        /// <param name="errorLocalizer">Localizer under test.</param>
        /// <param name="cultureNames">Culture names to check (e.g. "ru", "en").</param>
        /// <exception cref="DomainAssertException">At least one code has no working translation.</exception>
        public static void AssertAllLocalized(ErrorCatalog catalog, IErrorLocalizer errorLocalizer, params string[] cultureNames)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(errorLocalizer);
            ArgumentNullException.ThrowIfNull(cultureNames);
            if (cultureNames.Length == 0)
                throw new ArgumentException("At least one culture is required.", nameof(cultureNames));

            List<string>? missing = null;

            foreach (var cultureName in cultureNames)
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                foreach (var descriptor in catalog.All)
                {
                    // Probing with the template's placeholder count exercises the localized
                    // template's formatting too, so a broken translation fails here instead of
                    // becoming a 500 on the first real request.
                    string? localized;
                    try
                    {
                        localized = errorLocalizer.Localize(descriptor.CreateError(PlaceholderArguments(descriptor.MessageTemplate)), culture);
                    }
                    catch (FormatException)
                    {
                        localized = null;
                    }

                    if (localized == null)
                        (missing ??= []).Add($"[{cultureName}] {descriptor.Code}");
                }
            }

            if (missing != null)
                throw DomainAssert.Failure($"Missing or broken error localizations: {string.Join("; ", missing)}");
        }

        static object?[] PlaceholderArguments(string messageTemplate)
        {
            var count = 0;
            for (var i = 0; i < messageTemplate.Length - 1; i++)
            {
                if (messageTemplate[i] != '{' || !char.IsAsciiDigit(messageTemplate[i + 1]))
                    continue;

                var index = 0;
                var position = i + 1;
                while (position < messageTemplate.Length && char.IsAsciiDigit(messageTemplate[position]))
                {
                    index = index * 10 + (messageTemplate[position] - '0');
                    position++;
                }

                count = Math.Max(count, index + 1);
            }

            if (count == 0)
                return [];

            var arguments = new object?[count];
            for (var i = 0; i < count; i++)
                arguments[i] = i;

            return arguments;
        }
    }
}
