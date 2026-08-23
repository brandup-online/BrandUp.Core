using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BrandUp
{
    /// <summary>
    /// <see cref="ValidationProblemDetails"/> that serializes <c>errors</c> as the package's
    /// uniform list of <see cref="ProblemError"/> instead of the framework's per-member
    /// dictionary. Model-state keys become <see cref="ProblemError.Members"/> (they name request
    /// members under model binding and <c>[ApiController]</c> validation); a model-level error
    /// (empty key) carries no members. Declared as a <see cref="ValidationProblemDetails"/>
    /// subclass so it flows through <c>ValidationProblem(...)</c> and API-behavior conventions
    /// unchanged; the <see langword="new"/> property wins at serialization because serializers
    /// bind to the runtime type.
    /// </summary>
    public class DomainValidationProblemDetails : ValidationProblemDetails
    {
        /// <summary>
        /// Creates the problem from model state, one <see cref="ProblemError"/> per model-state
        /// message.
        /// </summary>
        /// <param name="modelState">Model state to take the errors from.</param>
        public DomainValidationProblemDetails(ModelStateDictionary modelState)
        {
            ArgumentNullException.ThrowIfNull(modelState);

            List<ProblemError> errors = [];
            foreach (var (key, entry) in modelState)
            {
                foreach (var error in entry.Errors)
                {
                    // The framework maps an exception without a message to an empty string;
                    // an empty message is useless to a client, so name the member at least.
                    var message = !string.IsNullOrEmpty(error.ErrorMessage)
                        ? error.ErrorMessage
                        : $"The value of '{key}' is invalid.";

                    errors.Add(new ProblemError(string.Empty, message, key.Length > 0 ? [key] : null));
                }
            }

            Errors = errors;
            FillBaseErrors(errors);
        }

        /// <summary>
        /// Creates the problem from prebuilt errors.
        /// </summary>
        /// <param name="errors">Errors of the response.</param>
        public DomainValidationProblemDetails(IReadOnlyList<ProblemError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            Errors = errors;
            FillBaseErrors(errors);
        }

        /// <summary>
        /// Errors of the response, in the package's uniform shape.
        /// </summary>
        public new IReadOnlyList<ProblemError> Errors { get; }

        // The inherited dictionary is what every consumer reading this object as a plain
        // ValidationProblemDetails sees — filters, logging, a custom problem-details writer. It
        // never reaches the JSON (the shadowing property does), so keeping it in sync costs
        // nothing and spares those consumers a silently empty collection.
        void FillBaseErrors(IReadOnlyList<ProblemError> errors)
        {
            var baseErrors = base.Errors;

            foreach (var error in errors)
            {
                foreach (var member in error.Members ?? [string.Empty])
                {
                    baseErrors[member] = baseErrors.TryGetValue(member, out var messages)
                        ? [.. messages, error.Message]
                        : [error.Message];
                }
            }
        }
    }
}
