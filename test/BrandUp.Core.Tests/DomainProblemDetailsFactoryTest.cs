using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandUp
{
    public class DomainProblemDetailsFactoryTest
    {
        readonly DomainProblemDetailsFactory factory = new();
        readonly DefaultHttpContext httpContext = new() { TraceIdentifier = "trace-1" };

        public DomainProblemDetailsFactoryTest()
        {
            httpContext.Request.Path = "/v1/channel";
        }

        [Fact]
        public void CreateProblemDetails_AppliesDefaults()
        {
            var problemDetails = factory.CreateProblemDetails(httpContext, StatusCodes.Status403Forbidden, title: "Not allowed.", detail: "Tariff.");

            Assert.Equal(403, problemDetails.Status);
            Assert.Equal("Not allowed.", problemDetails.Title);
            Assert.Equal("Tariff.", problemDetails.Detail);
            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.4", problemDetails.Type);
            Assert.Equal("/v1/channel", problemDetails.Instance);
            Assert.NotNull(problemDetails.Extensions["traceId"]);
        }

        [Fact]
        public void CreateProblemDetails_DefaultsTo500()
        {
            Assert.Equal(500, factory.CreateProblemDetails(httpContext).Status);
        }

        [Fact]
        public void CreateProblemDetails_FillsTheTitleOfTheStatus()
        {
            // The framework's factory titles a title-less problem by its status; so does this one.
            Assert.Equal("Conflict", factory.CreateProblemDetails(httpContext, StatusCodes.Status409Conflict).Title);
            Assert.Equal("Unprocessable Entity", factory.CreateProblemDetails(httpContext, StatusCodes.Status422UnprocessableEntity).Title);

            // 422 is an RFC 4918 status — the type URI must not claim RFC 9110.
            Assert.Equal("https://tools.ietf.org/html/rfc4918#section-11.2",
                factory.CreateProblemDetails(httpContext, StatusCodes.Status422UnprocessableEntity).Type);
        }

        [Fact]
        public void CreateValidationProblemDetails_ErrorsAreTheUniformList()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Phone", "Phone is required.");
            modelState.AddModelError(string.Empty, "Model is invalid.");

            var problemDetails = Assert.IsType<DomainValidationProblemDetails>(
                factory.CreateValidationProblemDetails(httpContext, modelState));

            Assert.Equal(400, problemDetails.Status);
            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problemDetails.Type);
            Assert.Contains(problemDetails.Errors, it => it.Message == "Phone is required." && it.Members!.Single() == "Phone");
            Assert.Contains(problemDetails.Errors, it => it.Message == "Model is invalid." && it.Members == null);
        }

        [Fact]
        public void CreateValidationProblemDetails_InheritedDictionaryStaysInSync()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Phone", "Phone is required.");
            modelState.AddModelError("Phone", "Phone is too short.");
            modelState.AddModelError(string.Empty, "Model is invalid.");

            // Consumers reading the response as a plain ValidationProblemDetails — filters,
            // logging, a custom problem-details writer — must not see an empty collection.
            var problemDetails = (ValidationProblemDetails)factory.CreateValidationProblemDetails(httpContext, modelState);

            Assert.Equal(["Phone is required.", "Phone is too short."], problemDetails.Errors["Phone"]);
            Assert.Equal(["Model is invalid."], problemDetails.Errors[string.Empty]);
        }

        [Fact]
        public void CreateValidationProblemDetails_SerializesErrorsAsList()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Phone", "Phone is required.");

            var problemDetails = factory.CreateValidationProblemDetails(httpContext, modelState);
            var json = System.Text.Json.JsonSerializer.Serialize<object>(problemDetails,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            // The runtime type wins: errors is the uniform list, not the base dictionary.
            Assert.Contains("\"errors\":[{\"code\":\"\",\"message\":\"Phone is required.\",\"members\":[\"Phone\"]}]", json);
            Assert.Contains("\"traceId\"", json);
        }

        [Fact]
        public void CreateValidationProblemDetails_MessagelessEntryStillNamesTheMember()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Phone", string.Empty);

            var problemDetails = Assert.IsType<DomainValidationProblemDetails>(
                factory.CreateValidationProblemDetails(httpContext, modelState));

            var error = Assert.Single(problemDetails.Errors);
            Assert.Equal("The value of 'Phone' is invalid.", error.Message);
        }

        [Fact]
        public void AddDomainProblemDetails_ReplacesTheFactory()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ProblemDetailsFactory>(new DomainProblemDetailsFactory());

            services.AddDomainProblemDetails();

            var descriptor = Assert.Single(services, it => it.ServiceType == typeof(ProblemDetailsFactory));
            Assert.Equal(typeof(DomainProblemDetailsFactory), descriptor.ImplementationType);
        }
    }
}
