using System.Globalization;
using BrandUp.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

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
                    var message = error.LocalizeOrInvariant(errorLocalizer, culture);

                    return error is ValidationError validationError
                        ? (object)new { code = error.Code, message, members = validationError.MemberNames }
                        : new { code = error.Code, message };
                })
                .ToArray();

            return problemDetails;
        }

        /// <summary>
        /// Maps the result to a minimal-API <see cref="IResult"/>: 204 No Content on success, or a
        /// problem response built by <see cref="ToProblemDetails(Result, IErrorLocalizer?, CultureInfo?)"/>.
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
        /// or a problem response built by <see cref="ToProblemDetails(Result, IErrorLocalizer?, CultureInfo?)"/>.
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

        /// <summary>
        /// Builds a <see cref="ProblemDetails"/> from a failed result, resolving the registered
        /// <see cref="IErrorLocalizer"/> (if any) from the request services and localizing for
        /// the current request culture.
        /// </summary>
        /// <param name="result">Failed result.</param>
        /// <param name="httpContext">Current HTTP context.</param>
        /// <exception cref="InvalidOperationException">The result is successful.</exception>
        public static ProblemDetails ToProblemDetails(this Result result, HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            return result.ToProblemDetails(ResolveLocalizer(httpContext));
        }

        /// <summary>
        /// Maps the result to a minimal-API <see cref="IResult"/>, resolving the registered
        /// <see cref="IErrorLocalizer"/> (if any) from the request services.
        /// </summary>
        /// <param name="result">Result to map.</param>
        /// <param name="httpContext">Current HTTP context.</param>
        public static IResult ToHttpResult(this Result result, HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            return result.ToHttpResult(ResolveLocalizer(httpContext));
        }

        /// <summary>
        /// Maps the result to a minimal-API <see cref="IResult"/> with data, resolving the
        /// registered <see cref="IErrorLocalizer"/> (if any) from the request services.
        /// </summary>
        /// <typeparam name="TData">Type of the carried data.</typeparam>
        /// <param name="result">Result to map.</param>
        /// <param name="httpContext">Current HTTP context.</param>
        public static IResult ToHttpResult<TData>(this Result<TData> result, HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            return result.ToHttpResult(ResolveLocalizer(httpContext));
        }

        /// <summary>
        /// Maps the result to an MVC <see cref="ActionResult"/>, resolving the registered
        /// <see cref="IErrorLocalizer"/> (if any) from the request services.
        /// </summary>
        /// <param name="result">Result to map.</param>
        /// <param name="httpContext">Current HTTP context.</param>
        public static ActionResult ToActionResult(this Result result, HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            return result.ToActionResult(ResolveLocalizer(httpContext));
        }

        /// <summary>
        /// Maps the result to an MVC <see cref="ActionResult{TValue}"/>, resolving the registered
        /// <see cref="IErrorLocalizer"/> (if any) from the request services.
        /// </summary>
        /// <typeparam name="TData">Type of the carried data.</typeparam>
        /// <param name="result">Result to map.</param>
        /// <param name="httpContext">Current HTTP context.</param>
        public static ActionResult<TData> ToActionResult<TData>(this Result<TData> result, HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            return result.ToActionResult(ResolveLocalizer(httpContext));
        }

        /// <summary>
        /// Adds the errors of a failed result to MVC model state, resolving the registered
        /// <see cref="IErrorLocalizer"/> (if any) from the request services (see
        /// <see cref="AddToModelState(Result, ModelStateDictionary, IErrorLocalizer?, CultureInfo?)"/>).
        /// </summary>
        /// <param name="result">Failed result.</param>
        /// <param name="modelState">Model state to add the errors to.</param>
        /// <param name="httpContext">Current HTTP context.</param>
        /// <exception cref="InvalidOperationException">The result is successful.</exception>
        public static void AddToModelState(this Result result, ModelStateDictionary modelState, HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            result.AddToModelState(modelState, ResolveLocalizer(httpContext));
        }

        // The one place that knows how the HttpContext overloads obtain the localizer.
        static IErrorLocalizer? ResolveLocalizer(HttpContext httpContext)
        {
            return httpContext.RequestServices.GetService<IErrorLocalizer>();
        }

        /// <summary>
        /// Adds the errors of a failed result to MVC model state so the controller can answer
        /// with <c>ValidationProblem()</c> — the same <see cref="ValidationProblemDetails"/>
        /// contract as <c>[ApiController]</c> model validation. For APIs that keep their own
        /// established error format and status mapping, taking only the stable codes and
        /// localization from the domain. Validation errors carrying member names are keyed by
        /// each member (matching automatic model validation); every other error is keyed by its
        /// <see cref="IError.Code"/>, which is empty for a member-less validation error and lands
        /// it under the model-level key, again as automatic validation does.
        /// </summary>
        /// <remarks>
        /// <see cref="IError.Kind"/> is deliberately ignored: model state has no place to carry it
        /// and <c>ValidationProblem()</c> always answers 400. Route the kinds that are not about
        /// the request — <see cref="ErrorKind.NotFound"/>, <see cref="ErrorKind.Unauthorized"/>,
        /// <see cref="ErrorKind.Forbidden"/>, <see cref="ErrorKind.Conflict"/>,
        /// <see cref="ErrorKind.Internal"/> — to their own status before calling this, or an
        /// internal failure is reported to the client as a validation problem (see
        /// <see cref="ToHttpStatusCode"/>).
        /// </remarks>
        /// <param name="result">Failed result.</param>
        /// <param name="modelState">Model state to add the errors to.</param>
        /// <param name="errorLocalizer">Localizer resolving messages by error code; <see langword="null"/> keeps invariant messages.</param>
        /// <param name="culture">Target culture; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        /// <exception cref="InvalidOperationException">The result is successful.</exception>
        public static void AddToModelState(this Result result, ModelStateDictionary modelState, IErrorLocalizer? errorLocalizer = null, CultureInfo? culture = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(modelState);
            if (result.IsSuccess)
                throw new InvalidOperationException("A successful result has no errors to add.");

            // Resolved once so every message of one response speaks the same culture.
            culture ??= CultureInfo.CurrentUICulture;

            foreach (var error in result.Errors)
            {
                var message = error.LocalizeOrInvariant(errorLocalizer, culture);
                var keyedByMember = false;

                if (error is ValidationError validationError)
                {
                    // One pass, and blank names are skipped rather than handed to AddModelError:
                    // it throws on a null key, which would turn the error response into a 500.
                    foreach (var memberName in validationError.MemberNames)
                    {
                        if (string.IsNullOrEmpty(memberName))
                            continue;

                        modelState.AddModelError(memberName, message);
                        keyedByMember = true;
                    }
                }

                if (!keyedByMember)
                    modelState.AddModelError(error.Code, message);
            }
        }

        /// <summary>
        /// The HTTP status the package maps the error kind to. Exposed so an API that keeps its
        /// own response format still derives statuses from the one mapping table, instead of a
        /// copied switch that silently answers 400 for every kind added to the enum later.
        /// </summary>
        /// <param name="kind">Semantic category of the error.</param>
        public static int ToHttpStatusCode(this ErrorKind kind) => MapKind(kind).StatusCode;

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
