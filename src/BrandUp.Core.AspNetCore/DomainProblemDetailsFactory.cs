using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BrandUp
{
    /// <summary>
    /// <see cref="ProblemDetailsFactory"/> producing the package's uniform problem format, so
    /// every MVC-built error response — <c>[ApiController]</c> model validation,
    /// <c>Problem(...)</c>, <c>ValidationProblem(...)</c> — matches the responses built from
    /// domain results by <see cref="ResultHttpExtensions"/>: RFC 9457 fields, a <c>traceId</c>
    /// extension and validation errors as a list of <see cref="ProblemError"/> instead of the
    /// framework's per-member dictionary. Register with
    /// <see cref="DomainProblemDetailsExtensions.AddDomainProblemDetails(IServiceCollection)"/>.
    /// </summary>
    public sealed class DomainProblemDetailsFactory : ProblemDetailsFactory
    {
        /// <inheritdoc/>
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null,
            string? title = null, string? type = null, string? detail = null, string? instance = null)
        {
            var problemDetails = new ProblemDetails
            {
                Status = statusCode ?? StatusCodes.Status500InternalServerError,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };

            ProblemDetailsDefaults.Apply(problemDetails, httpContext);

            return problemDetails;
        }

        /// <inheritdoc/>
        public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext,
            ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null,
            string? type = null, string? detail = null, string? instance = null)
        {
            ArgumentNullException.ThrowIfNull(modelStateDictionary);

            var problemDetails = new DomainValidationProblemDetails(modelStateDictionary)
            {
                Status = statusCode ?? StatusCodes.Status400BadRequest,
                Type = type,
                Detail = detail,
                Instance = instance
            };

            // The base class defaults the title; overwrite only an explicit one.
            if (title != null)
                problemDetails.Title = title;

            ProblemDetailsDefaults.Apply(problemDetails, httpContext);

            return problemDetails;
        }
    }

    /// <summary>
    /// Registration extensions for <see cref="DomainProblemDetailsFactory"/>.
    /// </summary>
    public static class DomainProblemDetailsExtensions
    {
        /// <summary>
        /// Makes <see cref="DomainProblemDetailsFactory"/> the application's
        /// <see cref="ProblemDetailsFactory"/>, giving every MVC error response the package's
        /// uniform problem format. Call in any order relative to <c>AddControllers</c>.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public static IServiceCollection AddDomainProblemDetails(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.Replace(ServiceDescriptor.Singleton<ProblemDetailsFactory, DomainProblemDetailsFactory>());

            return services;
        }
    }
}
