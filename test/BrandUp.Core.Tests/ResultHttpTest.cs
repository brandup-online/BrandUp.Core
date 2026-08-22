using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BrandUp.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
            var errors = Assert.IsType<object[]>(problemDetails.Extensions["errors"]);
            Assert.Single(errors);
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
