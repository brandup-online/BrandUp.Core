using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BrandUp
{
    /// <summary>
    /// Endpoint publishing the <see cref="ErrorCatalog"/> as machine-readable documentation for
    /// API integrators: every error code with its category, HTTP status, message (localized to
    /// the request culture when an <see cref="IErrorLocalizer"/> is registered) and description.
    /// </summary>
    public static class ErrorCatalogEndpoint
    {
        // Culture names come from the request (Accept-Language drives CurrentUICulture) -
        // without a cap a scanner cycling culture names would grow the model cache for the
        // process lifetime. Cultures beyond the cap are built per request, uncached.
        const int CultureCacheLimit = 64;

        /// <summary>
        /// Maps a GET endpoint returning the registered error catalog. The catalog is immutable
        /// after startup, so the built model is cached per culture.
        /// </summary>
        /// <param name="endpoints">Endpoint route builder.</param>
        /// <param name="pattern">Route pattern of the endpoint.</param>
        /// <returns>The endpoint convention builder.</returns>
        public static IEndpointConventionBuilder MapErrorCatalog(this IEndpointRouteBuilder endpoints, string pattern = "/api/errors")
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var modelCache = new ConcurrentDictionary<string, ErrorCatalogEntry[]>();

            return endpoints.MapGet(pattern, (HttpContext httpContext) =>
            {
                // Resolved from request services (not lambda-bound) so a missing registration
                // fails with a clear message instead of an obscure body-binding error.
                var catalog = httpContext.RequestServices.GetService<ErrorCatalog>()
                    ?? throw new InvalidOperationException($"{nameof(ErrorCatalog)} is not registered. Configure the domain with AddErrorCatalog before mapping the endpoint.");
                var errorLocalizer = httpContext.RequestServices.GetService<IErrorLocalizer>();
                var culture = CultureInfo.CurrentUICulture;

                if (!modelCache.TryGetValue(culture.Name, out var model))
                {
                    model = BuildModel(catalog, errorLocalizer, culture);
                    if (modelCache.Count < CultureCacheLimit)
                        modelCache.TryAdd(culture.Name, model);
                }

                return Results.Ok(model);
            });
        }

        internal static ErrorCatalogEntry[] BuildModel(ErrorCatalog catalog, IErrorLocalizer? errorLocalizer, CultureInfo culture)
        {
            return [.. catalog.All
                .OrderBy(descriptor => descriptor.Code, StringComparer.Ordinal)
                .Select(descriptor =>
                {
                    var message = descriptor.CreateError().LocalizeOrInvariant(errorLocalizer, culture);

                    return new ErrorCatalogEntry(
                        descriptor.Code,
                        descriptor.Kind.ToString(),
                        ResultHttpExtensions.MapKind(descriptor.Kind).StatusCode,
                        message,
                        descriptor.Description);
                })];
        }
    }

    /// <summary>
    /// One row of the published error catalog.
    /// </summary>
    /// <param name="Code">Stable machine-readable error code.</param>
    /// <param name="Kind">Semantic category name.</param>
    /// <param name="HttpStatus">HTTP status the code maps to.</param>
    /// <param name="Message">Message template, localized to the request culture when possible.</param>
    /// <param name="Description">When and why the error occurs.</param>
    public sealed record ErrorCatalogEntry(string Code, string Kind, int HttpStatus, string Message, string? Description);
}
