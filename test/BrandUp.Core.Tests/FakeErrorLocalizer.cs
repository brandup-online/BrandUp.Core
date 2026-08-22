using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BrandUp
{
    // Dictionary-backed IErrorLocalizer: culture name -> code -> template.
    sealed class FakeErrorLocalizer(Dictionary<string, Dictionary<string, string>> resources) : IErrorLocalizer
    {
        public string Localize(IError error, CultureInfo culture)
        {
            if (!resources.TryGetValue(culture.Name, out var templates) || !templates.TryGetValue(error.Code, out var template))
                return null;

            return error.Arguments.Count > 0 ? string.Format(culture, template, error.Arguments.ToArray()) : template;
        }
    }
}
