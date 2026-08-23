using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BrandUp.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace BrandUp
{
    public class ResultHttpTest
    {
        [Fact]
        public void ToProblemDetails_MapsErrorKindToStatus()
        {
            Assert.Equal(404, Result.Error("code", "message", ErrorKind.NotFound).ToProblemDetails().Status);
            Assert.Equal(403, Result.Error("code", "message", ErrorKind.Forbidden).ToProblemDetails().Status);
            Assert.Equal(409, Result.Error("code", "message", ErrorKind.Conflict).ToProblemDetails().Status);
            Assert.Equal(500, Result.Error("code", "message", ErrorKind.Internal).ToProblemDetails().Status);
            Assert.Equal(400, Result.Error("code", "message").ToProblemDetails().Status);
        }

        [Fact]
        public void ToProblemDetails_ValidationErrors()
        {
            var result = Result.Error([new ValidationError("Phone is required.", ["Phone"])]);

            var problemDetails = result.ToProblemDetails();

            Assert.Equal(400, problemDetails.Status);
            var errors = Assert.IsType<ProblemError[]>(problemDetails.Extensions["errors"]);
            var error = Assert.Single(errors);
            Assert.Equal(string.Empty, error.Code);
            Assert.Equal("Phone is required.", error.Message);
            Assert.Equal("Phone", Assert.Single(error.Members!));
        }

        [Fact]
        public void ToProblemDetails_UniformErrorShape_MembersOnlyForValidation()
        {
            var result = Result.Error(
            [
                new Error("channel-not-found", "Channel not found.", ErrorKind.NotFound),
                new ValidationError("Start must precede end.", [])
            ]);

            var errors = Assert.IsType<ProblemError[]>(result.ToProblemDetails().Extensions["errors"]);

            Assert.Equal(("channel-not-found", "Channel not found."), (errors[0].Code, errors[0].Message));
            Assert.Null(errors[1].Members); // member-less validation error carries no members list
        }

        [Fact]
        public void ToProblemDetails_BlankMemberNamesAreDropped()
        {
            var result = Result.Error([new ValidationError("Start must precede end.", ["", "End"])]);

            var error = Assert.Single(Assert.IsType<ProblemError[]>(result.ToProblemDetails().Extensions["errors"]));

            // A member the client cannot address is noise: only the named one survives.
            Assert.Equal("End", Assert.Single(error.Members!));
        }

        [Fact]
        public void ToProblemDetails_OnlyBlankMemberNamesCarryNoMembers()
        {
            var result = Result.Error([new ValidationError("Start must precede end.", ["", ""])]);

            var error = Assert.Single(Assert.IsType<ProblemError[]>(result.ToProblemDetails().Extensions["errors"]));

            Assert.Null(error.Members);
        }

        [Fact]
        public void ToProblemDetails_AppliesRfc9457Defaults()
        {
            var problemDetails = Result.Error("code", "message", ErrorKind.NotFound).ToProblemDetails();

            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", problemDetails.Type);
        }

        [Fact]
        public void ToProblemDetails_TraceIdComesFromTheCurrentActivity()
        {
            using var activity = new System.Diagnostics.Activity("test").Start();

            var problemDetails = Result.Error("code", "message").ToProblemDetails();

            Assert.Equal(activity.Id, problemDetails.Extensions["traceId"]);
        }

        [Fact]
        public void ToProblemDetails_ExplicitValuesSurviveTheDefaults()
        {
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { TraceIdentifier = "trace-1" };
            httpContext.Request.Path = "/v1/channel";

            var problemDetails = Result.Error("code", "message", ErrorKind.NotFound).ToProblemDetails(httpContext);
            problemDetails.Type = "https://errors.example/not-found";
            problemDetails.Title = "Custom title.";
            problemDetails.Instance = "urn:custom";

            // Applying the defaults again must not overwrite what the caller decided.
            Result.Error("code", "message", ErrorKind.NotFound).ToProblemDetails(httpContext);

            Assert.Equal("https://errors.example/not-found", problemDetails.Type);
            Assert.Equal("Custom title.", problemDetails.Title);
            Assert.Equal("urn:custom", problemDetails.Instance);
        }

        [Fact]
        public void ToProblemDetails_InstanceIncludesThePathBase()
        {
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            httpContext.Request.PathBase = "/api";
            httpContext.Request.Path = "/v1/channel";

            var problemDetails = Result.Error("code", "message").ToProblemDetails(httpContext);

            Assert.Equal("/api/v1/channel", problemDetails.Instance);
        }

        [Fact]
        public void ToProblemDetails_WithoutRequestServices()
        {
            // A context built outside the request pipeline has no container; reporting an error
            // must not throw because the optional localizer cannot be resolved.
            var problemDetails = Result.Error("code", "message", ErrorKind.Conflict)
                .ToProblemDetails(new Microsoft.AspNetCore.Http.DefaultHttpContext());

            Assert.Equal(409, problemDetails.Status);
        }

        [Fact]
        public void ToActionResult_Success()
        {
            Assert.IsType<NoContentResult>(Result.Success().ToActionResult());

            var typed = Result.Success(42).ToActionResult();
            var okResult = Assert.IsType<OkObjectResult>(typed.Result);
            Assert.Equal(42, okResult.Value);
        }

        [Fact]
        public void ToActionResult_Error()
        {
            var actionResult = Result.Error("code", "message", ErrorKind.NotFound).ToActionResult();

            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(404, objectResult.StatusCode);
            Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("application/problem+json", Assert.Single(objectResult.ContentTypes));
        }

        [Fact]
        public void AddToModelState_KeysValidationByMemberAndOthersByCode()
        {
            var result = Result.Error(
            [
                new ValidationError("Phone is required.", ["Phone"]),
                new Error("channel-not-found", "Channel not found.", ErrorKind.Validation)
            ]);
            var modelState = new ModelStateDictionary();

            result.AddToModelState(modelState);

            Assert.Equal("Phone is required.", Assert.Single(modelState["Phone"]!.Errors).ErrorMessage);
            Assert.Equal("Channel not found.", Assert.Single(modelState["channel-not-found"]!.Errors).ErrorMessage);
        }

        [Fact]
        public void AddToModelState_PassesLocalizerAndCultureThrough()
        {
            var localizer = new FakeErrorLocalizer(new() { ["ru"] = new() { ["channel-not-found"] = "Канал не найден." } });
            var result = Result.Error("channel-not-found", "Channel not found.", ErrorKind.Validation);
            var modelState = new ModelStateDictionary();

            result.AddToModelState(modelState, localizer, CultureInfo.GetCultureInfo("ru"));

            Assert.Equal("Канал не найден.", Assert.Single(modelState["channel-not-found"]!.Errors).ErrorMessage);
        }

        [Fact]
        public void AddToModelState_MemberLessValidationError_UsesModelLevelKey()
        {
            // Class-level validation attributes produce no member names, and the code of a
            // validation error is empty: both errors belong under the model-level key.
            var result = Result.Error(
            [
                new ValidationError("Start must precede end.", []),
                new ValidationError("Period is too long.", null)
            ]);
            var modelState = new ModelStateDictionary();

            result.AddToModelState(modelState);

            Assert.Equal(2, modelState[string.Empty]!.Errors.Count);
        }

        [Fact]
        public void AddToModelState_BlankMemberNames_AreSkippedNotThrown()
        {
            // AddModelError throws on a null key: a bad member name must not turn the error
            // response into a 500, and the error must still reach model state.
            var result = Result.Error([new ValidationError("Phone is required.", [null!, string.Empty])]);
            var modelState = new ModelStateDictionary();

            result.AddToModelState(modelState);

            Assert.Equal("Phone is required.", Assert.Single(modelState[string.Empty]!.Errors).ErrorMessage);
        }

        [Fact]
        public void AddToModelState_LazyMemberNames_AreNotConsumedByTheProbe()
        {
            var callCount = 0;
            IEnumerable<string> LazyNames()
            {
                callCount++;
                yield return "Phone";
            }

            var result = Result.Error([new ValidationError("Phone is required.", LazyNames())]);
            var modelState = new ModelStateDictionary();

            result.AddToModelState(modelState);

            Assert.Equal("Phone is required.", Assert.Single(modelState["Phone"]!.Errors).ErrorMessage);
            Assert.Equal(1, callCount); // materialized once, at construction
        }

        [Fact]
        public void AddToModelState_SuccessThrows()
        {
            Assert.Throws<InvalidOperationException>(() => Result.Success().AddToModelState(new ModelStateDictionary()));
        }

        [Fact]
        public void ToHttpStatusCode_MatchesTheProblemDetailsMapping()
        {
            Assert.Equal(404, ErrorKind.NotFound.ToHttpStatusCode());
            Assert.Equal(401, ErrorKind.Unauthorized.ToHttpStatusCode());
            Assert.Equal(500, ErrorKind.Internal.ToHttpStatusCode());
            Assert.Equal(400, ErrorKind.Validation.ToHttpStatusCode());
        }

        [Fact]
        public void DomainError_TakesTypedResultsWithoutACast()
        {
            var controller = new TestController { ControllerContext = new() { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };

            // A typed result upcasts to Result here: the point of the helper.
            IActionResult actionResult = controller.DomainError(Result.Error<int>("code", "message", ErrorKind.Conflict));

            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(409, objectResult.StatusCode);
            Assert.Equal("application/problem+json", Assert.Single(objectResult.ContentTypes));
        }

        [Fact]
        public void DomainError_SuccessThrows()
        {
            var controller = new TestController { ControllerContext = new() { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() } } };

            Assert.Throws<InvalidOperationException>(() => controller.DomainError(Result.Success()));
        }

        class TestController : ControllerBase { }

        [Fact]
        public void ToHttpResult_MapsBothBranches()
        {
            Assert.IsType<NoContent>(Result.Success().ToHttpResult());

            var okResult = Assert.IsType<Ok<int>>(Result.Success(42).ToHttpResult());
            Assert.Equal(42, okResult.Value);

            var problemResult = Assert.IsType<ProblemHttpResult>(Result.Error("code", "message", ErrorKind.Conflict).ToHttpResult());
            Assert.Equal(409, problemResult.StatusCode);
        }
    }
}
