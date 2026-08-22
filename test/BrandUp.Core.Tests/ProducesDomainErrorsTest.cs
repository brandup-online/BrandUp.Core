using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace BrandUp
{
    public class ProducesDomainErrorsTest
    {
        sealed class CapturingConventionBuilder : IEndpointConventionBuilder
        {
            public List<Action<EndpointBuilder>> Conventions { get; } = [];

            public void Add(Action<EndpointBuilder> convention) => Conventions.Add(convention);
        }

        [Fact]
        public void AddsProblemResponsePerDistinctStatus()
        {
            var builder = new CapturingConventionBuilder();
            builder.ProducesDomainErrors(ExampleErrors.OrderNotFound, ExampleErrors.OrderAlreadyPaid);

            var endpointBuilder = new RouteEndpointBuilder(null, RoutePatternFactory.Parse("/orders"), 0);
            foreach (var convention in builder.Conventions)
                convention(endpointBuilder);

            var declared = Assert.IsType<DomainErrorsMetadata>(Assert.Single(endpointBuilder.Metadata.OfType<DomainErrorsMetadata>()));
            Assert.Equal(2, declared.Errors.Count);

            var statuses = endpointBuilder.Metadata.OfType<ProducesResponseTypeMetadata>().Select(metadata => metadata.StatusCode).OrderBy(status => status).ToArray();
            Assert.Equal([404, 409], statuses);
        }

        [Fact]
        public void EmptyDescriptors_Throw()
        {
            var builder = new CapturingConventionBuilder();

            Assert.Throws<ArgumentException>(() => builder.ProducesDomainErrors());
        }
    }
}
