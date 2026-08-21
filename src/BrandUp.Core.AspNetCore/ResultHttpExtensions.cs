using System.Globalization;
using BrandUp.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BrandUp
{
    /// <summary>
    /// Maps <see cref="Result"/>/<see cref="Result{TData}"/> to HTTP responses. The status code is
    /// derived from the <see cref="ErrorKind"/> of the first error: Validation → 400, NotFound →
    /// 404, Unauthorized → 401, Forbidden → 403, Conflict → 409, Internal → 500, Unspecified → 400.
    /// When an <see cref="IErrorLocalizer"/> is passed, error messages are localized for the given
    /// culture (<see cref="CultureInfo.CurrentUICulture"/> by default — the request culture under
    /// request localization), falling back to the invariant message.
    /// </summary>
    public static class ResultHttpExtensions
    {
        /// <summary>
        /// Builds a <see cref="ProblemDetails"/> from a failed result: the mapped status, a title
        /// from the error kind and an <c>errors</c> extension listing code/message pairs
        /// (validation errors also carry their member names).
        /// </summary>
        /// <param name="result">Failed result.</param>
        /// <param name="errorLocalizer">Localizer resolving messages by error code; <see langword="null"/> keeps invariant messages.</param>
        /// <param name="culture">Target culture; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        /// <exception cref="InvalidOperationException">The result is successful.</exception>
        public static ProblemDetails ToProblemDetails(this Result result, IErrorLocalizer? errorLocalizer = null, CultureInfo? culture = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (result.IsSuccess)
                throw new InvalidOperationException("A successful result has no problem to describe.");

            culture ??= CultureInfo.CurrentUICulture;

            var (statusCode, title) = MapKind(result.Errors.First().Kind);
            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title
            };

            problemDetails.Extensions["errors"] = result.Errors
                .Select(error =>
                {
                    var message = LocalizeOrInvariant(errorLocalizer, error, culture);

                    return error is CommandValidationError validationError
                        ? (object)new { code = error.Code, message, members = validationError.MemberNames }
                        : new { code = error.Code, message };
                })
                .ToArray();

            return problemDetails;
        }

        /// <summary>
        /// Maps the result to a minimal-API <see cref="IResult"/>: 204 No Content on success, or a
        /// problem response built by <see cref="ToProblemDetails"/>.
        /// </summary>
        /// <param name="result">Result to map.</param>
        /// <param name="errorLocalizer">Localizer resolving messages by error code.</param>
        /// <param name="culture">Target culture; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        public static IResult ToHttpResult(this Result result, IErrorLocalizer? errorLocalizer = null, CultureInfo? culture = null)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.ToProblemDetails(errorLocalizer, culture));
        }

        /// <summary>
        /// Maps the result to a minimal-API <see cref="IResult"/>: 200 OK with the data on success,
        /// or a problem response built by <see cref="ToProblemDetails"/>.
        /// </summary>
        /// <typeparam name="TData">Type of the carried data.</typeparam>
        /// <param name="result">Result to map.</param>
        /// <param name="errorLocalizer">Localizer resolving messages by error code.</param>
        /// <param name="culture">Target culture; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        public static IResult ToHttpResult<TData>(this Result<TData> result, IErrorLocalizer? errorLocalizer = null, CultureInfo? culture = null)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.IsSuccess ? Results.Ok(result.Data) : Results.Problem(result.ToProblemDetails(errorLocalizer, culture));
        }

        /// <summary>
        /// Maps the result to an MVC <see cref="ActionResult"/>: 204 No Content on success, or an
        /// object result carrying <see cref="ProblemDetails"/>.
        /// </summary>
        /// <param name="result">Result to map.</param>
        /// <param name="errorLocalizer">Localizer resolving messages by error code.</param>
        /// <param name="culture">Target culture; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        public static ActionResult ToActionResult(this Result result, IErrorLocalizer? errorLocalizer = null, CultureInfo? culture = null)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsSuccess)
                return new NoContentResult();

            var problemDetails = result.ToProblemDetails(errorLocalizer, culture);
            return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
        }

        /// <summary>
        /// Maps the result to an MVC <see cref="ActionResult{TValue}"/>: 200 OK with the data on
        /// success, or an object result carrying <see cref="ProblemDetails"/>.
        /// </summary>
        /// <typeparam name="TData">Type of the carried data.</typeparam>
        /// <param name="result">Result to map.</param>
        /// <param name="errorLocalizer">Localizer resolving messages by error code.</param>
        /// <param name="culture">Target culture; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        public static ActionResult<TData> ToActionResult<TData>(this Result<TData> result, IErrorLocalizer? errorLocalizer = null, CultureInfo? culture = null)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsSuccess)
                return new OkObjectResult(result.Data);

            var problemDetails = result.ToProblemDetails(errorLocalizer, culture);
            return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
        }

        // The one localize-with-fallback rule, shared with the error catalog endpoint.
        internal static string LocalizeOrInvariant(IErrorLocalizer? errorLocalizer, IError error, CultureInfo culture)
        {
            return errorLocalizer?.Localize(error, culture) ?? error.Message;
        }

        // Single mapping table for status and title: two parallel switches over ErrorKind
        // would drift when the enum grows. Every named member is listed explicitly; the discard
        // arm covers unnamed enum values only.
        internal static (int StatusCode, string Title) MapKind(ErrorKind kind) => kind switch
        {
            ErrorKind.Unspecified => (StatusCodes.Status400BadRequest, "The operation failed."),
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "The request failed validation."),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "The requested entity was not found."),
            ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "The request is not authenticated."),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "The operation is not allowed."),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "The operation conflicts with the current state."),
            ErrorKind.Internal => (StatusCodes.Status500InternalServerError, "An internal error occurred."),
            _ => (StatusCodes.Status400BadRequest, "The operation failed.")
        };
    }
}
