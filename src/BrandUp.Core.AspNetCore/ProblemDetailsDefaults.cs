using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BrandUp
{
    /// <summary>
    /// The RFC 9457 defaults every problem response of this package carries: a <c>type</c> URI
    /// pointing at the RFC 9110 status definition, an <c>instance</c> with the request path and
    /// a <c>traceId</c> extension for correlating the response with logs and traces — the same
    /// conventions ASP.NET Core's own problem responses follow, so responses built from domain
    /// results are indistinguishable in shape from framework-built ones.
    /// </summary>
    internal static class ProblemDetailsDefaults
    {
        internal const string ProblemContentType = "application/problem+json";

        /// <summary>
        /// Fills the fields the caller did not set: <c>type</c> from the status, <c>instance</c>
        /// from the request path and the <c>traceId</c> extension. Explicit values are respected.
        /// </summary>
        internal static void Apply(ProblemDetails problemDetails, HttpContext? httpContext)
        {
            if (problemDetails.Status is int status)
            {
                var (type, title) = ForStatus(status);

                problemDetails.Type ??= type;
                // The title is the human-readable half of RFC 9457: without it the package's
                // responses would be poorer than the framework's own, which fills it by status.
                problemDetails.Title ??= title;
            }

            // PathBase included: under a base path the request path alone is not the URL the
            // client called, and instance must identify the occurrence.
            if (httpContext != null)
                problemDetails.Instance ??= $"{httpContext.Request.PathBase}{httpContext.Request.Path}";

            // Activity id first — it correlates across services; the connection-local
            // TraceIdentifier is the fallback ASP.NET Core itself uses.
            var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;
            if (traceId != null)
                problemDetails.Extensions.TryAdd("traceId", traceId);
        }

        // The type URIs and titles ASP.NET Core itself uses for these statuses (its own
        // ProblemDetailsDefaults table), so responses of this package are indistinguishable from
        // the framework's. An unlisted status keeps a null type — RFC 9457 reads that as
        // "about:blank" — and a null title.
        static (string? Type, string? Title) ForStatus(int status) => status switch
        {
            StatusCodes.Status400BadRequest => ("https://tools.ietf.org/html/rfc9110#section-15.5.1", "Bad Request"),
            StatusCodes.Status401Unauthorized => ("https://tools.ietf.org/html/rfc9110#section-15.5.2", "Unauthorized"),
            StatusCodes.Status403Forbidden => ("https://tools.ietf.org/html/rfc9110#section-15.5.4", "Forbidden"),
            StatusCodes.Status404NotFound => ("https://tools.ietf.org/html/rfc9110#section-15.5.5", "Not Found"),
            StatusCodes.Status405MethodNotAllowed => ("https://tools.ietf.org/html/rfc9110#section-15.5.6", "Method Not Allowed"),
            StatusCodes.Status406NotAcceptable => ("https://tools.ietf.org/html/rfc9110#section-15.5.7", "Not Acceptable"),
            StatusCodes.Status408RequestTimeout => ("https://tools.ietf.org/html/rfc9110#section-15.5.9", "Request Timeout"),
            StatusCodes.Status409Conflict => ("https://tools.ietf.org/html/rfc9110#section-15.5.10", "Conflict"),
            StatusCodes.Status412PreconditionFailed => ("https://tools.ietf.org/html/rfc9110#section-15.5.13", "Precondition Failed"),
            StatusCodes.Status415UnsupportedMediaType => ("https://tools.ietf.org/html/rfc9110#section-15.5.16", "Unsupported Media Type"),
            // 422 is defined by RFC 4918, not RFC 9110 — the framework links the WebDAV section.
            StatusCodes.Status422UnprocessableEntity => ("https://tools.ietf.org/html/rfc4918#section-11.2", "Unprocessable Entity"),
            StatusCodes.Status426UpgradeRequired => ("https://tools.ietf.org/html/rfc9110#section-15.5.22", "Upgrade Required"),
            StatusCodes.Status429TooManyRequests => ("https://tools.ietf.org/html/rfc6585#section-4", "Too Many Requests"),
            StatusCodes.Status500InternalServerError => ("https://tools.ietf.org/html/rfc9110#section-15.6.1", "An error occurred while processing your request."),
            StatusCodes.Status502BadGateway => ("https://tools.ietf.org/html/rfc9110#section-15.6.3", "Bad Gateway"),
            StatusCodes.Status503ServiceUnavailable => ("https://tools.ietf.org/html/rfc9110#section-15.6.4", "Service Unavailable"),
            _ => (null, null)
        };
    }
}
