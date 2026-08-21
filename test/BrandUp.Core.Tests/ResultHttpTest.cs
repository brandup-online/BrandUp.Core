using System.Collections.Generic;
using System.Linq;
using BrandUp.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
            var result = Result.Error([new CommandValidationError("Phone is required.", ["Phone"])]);

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
