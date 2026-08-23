using Microsoft.AspNetCore.Mvc;

namespace BrandUp
{
    /// <summary>
    /// Controller-side shortcuts over the <see cref="Result"/> mappings.
    /// </summary>
    public static class ControllerBaseExtensions
    {
        /// <summary>
        /// The error response of a failed domain result: the problem response of
        /// <see cref="ResultHttpExtensions.ToActionResult(Result, Microsoft.AspNetCore.Http.HttpContext)"/>,
        /// with the registered <see cref="IErrorLocalizer"/> and the request culture.
        /// <para>
        /// Takes the non-generic <see cref="Result"/> on purpose: a <see cref="Result{TData}"/> upcasts
        /// to it here, whereas calling the extension directly on a typed result selects the generic
        /// overload and yields an <see cref="ActionResult{TValue}"/> — which an action declared as
        /// <see cref="IActionResult"/> cannot return. The failure is a compile error, so an action
        /// reporting a typed command's failure would otherwise need a cast at every call site.
        /// </para>
        /// </summary>
        /// <param name="controller">Controller handling the request.</param>
        /// <param name="result">Failed result.</param>
        /// <exception cref="InvalidOperationException">The result is successful — a success has no error to report.</exception>
        public static IActionResult DomainError(this ControllerBase controller, Result result)
        {
            ArgumentNullException.ThrowIfNull(controller);
            ArgumentNullException.ThrowIfNull(result);
            if (result.IsSuccess)
                throw new InvalidOperationException("A successful result has no error to report.");

            return result.ToActionResult(controller.HttpContext);
        }
    }
}
