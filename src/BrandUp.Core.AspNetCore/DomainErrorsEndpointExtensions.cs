using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BrandUp
{
    /// <summary>
    /// Endpoint metadata for the domain errors an operation can return, so OpenAPI documents
    /// (and client generators) see the error responses instead of only the success path.
    /// </summary>
    public static class DomainErrorsEndpointExtensions
    {
        /// <summary>
        /// Declares the cataloged errors this endpoint can return: emits a
        /// <c>application/problem+json</c> <see cref="ProblemDetails"/> response per distinct
        /// HTTP status the error kinds map to (via <see cref="ResultHttpExtensions.ToHttpStatusCode"/>,
        /// the same table the runtime mapping uses) and attaches
        /// <see cref="DomainErrorsMetadata"/> with the descriptors for documentation tooling.
        /// </summary>
        /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
        /// <param name="builder">Endpoint convention builder.</param>
        /// <param name="errors">Cataloged errors the endpoint can return.</param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="ArgumentException"><paramref name="errors"/> is empty or contains <see langword="null"/>.</exception>
        public static TBuilder ProducesDomainErrors<TBuilder>(this TBuilder builder, params ErrorDescriptor[] errors)
            where TBuilder : IEndpointConventionBuilder
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(errors);
            if (errors.Length == 0)
                throw new ArgumentException("At least one error descriptor is required.", nameof(errors));

            var descriptors = new ErrorDescriptor[errors.Length];
            for (var i = 0; i < errors.Length; i++)
                descriptors[i] = errors[i] ?? throw new ArgumentException("Error descriptors must not be null.", nameof(errors));

            builder.Add(endpointBuilder =>
            {
                endpointBuilder.Metadata.Add(new DomainErrorsMetadata(descriptors));

                foreach (var statusCode in descriptors.Select(descriptor => descriptor.Kind.ToHttpStatusCode()).Distinct())
                    endpointBuilder.Metadata.Add(new ProducesResponseTypeMetadata(statusCode, typeof(ProblemDetails), ["application/problem+json"]));
            });

            return builder;
        }
    }

    /// <summary>
    /// The cataloged errors an endpoint declared via
    /// <see cref="DomainErrorsEndpointExtensions.ProducesDomainErrors{TBuilder}"/> — available to
    /// OpenAPI transformers and documentation tooling through endpoint metadata.
    /// </summary>
    public sealed class DomainErrorsMetadata
    {
        internal DomainErrorsMetadata(IReadOnlyList<ErrorDescriptor> errors)
        {
            Errors = errors;
        }

        /// <summary>
        /// Errors the endpoint can return.
        /// </summary>
        public IReadOnlyList<ErrorDescriptor> Errors { get; }
    }
}
