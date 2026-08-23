using System.Text.Json.Serialization;

namespace BrandUp
{
    /// <summary>
    /// One error of a problem response — the item shape of the <c>errors</c> extension every
    /// transport helper of this package emits (<see cref="ResultHttpExtensions"/>,
    /// <see cref="DomainValidationProblemDetails"/>). A single shape keeps the API contract
    /// uniform: clients parse one structure regardless of whether the error came from a domain
    /// result, model validation or an exception.
    /// </summary>
    /// <param name="Code">Stable machine-readable error code; empty when the error has none.</param>
    /// <param name="Message">Human-readable message, localized at the transport edge when possible.</param>
    /// <param name="Members">Names of the request members the error relates to; omitted from the JSON when <see langword="null"/>.</param>
    public sealed record ProblemError(
        string Code,
        string Message,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Members = null);
}
