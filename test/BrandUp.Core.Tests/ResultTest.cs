using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandUp
{
    public class ResultTest
    {
        [Fact]
        public void Errors_EmptyOnSuccess()
        {
            var result = Result.Success();

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Errors);
            Assert.Empty(result.Errors);
            Assert.Equal(0, result.CountErrors);
        }

        [Fact]
        public void Errors_ContainsError()
        {
            var result = Result.Error("code", "message");

            Assert.False(result.IsSuccess);
            Assert.Equal(1, result.CountErrors);

            var error = result.Errors.Single();
            Assert.Equal("code", error.Code);
            Assert.Equal("message", error.Message);
        }

        [Fact]
        public void Error_MultipleErrors()
        {
            var result = Result.Error([new Error("a", "1"), new Error("b", "2")]);

            Assert.False(result.IsSuccess);
            Assert.Equal(2, result.CountErrors);
        }

        [Fact]
        public void AsObjectiveErrors_PreservesErrors()
        {
            var result = Result.Error("code", "message");

            var typed = result.AsObjectiveErrors<string>();

            Assert.False(typed.IsSuccess);
            Assert.Equal(1, typed.CountErrors);
            Assert.Equal("code", typed.Errors.Single().Code);
        }

        [Fact]
        public void AsObjectiveErrors_OnSuccess_Throws()
        {
            var result = Result.Success();

            Assert.Throws<ArgumentException>(() => result.AsObjectiveErrors<string>());
        }

        [Fact]
        public void ToBool_True()
        {
            var result = Result.Success();

            bool b = result;

            Assert.True(b);
            Assert.Equal("Success", result.ToString());
        }

        [Fact]
        public void ToBool_False()
        {
            var result = Result.Error(string.Empty, "Error message");

            bool b = result;

            Assert.False(b);
            Assert.Equal("Errors: 1", result.ToString());
        }

        [Fact]
        public void Log_Success()
        {
            var logger = NullLogger.Instance;
            var result = Result.Success();

            Assert.False(logger.LogIfError(result));
        }

        [Fact]
        public void Log_Error()
        {
            var logger = NullLogger.Instance;
            var result = Result.Error(string.Empty, "Error message");

            Assert.True(logger.LogIfError(result));
        }

        [Fact]
        public void Typed_null()
        {
            var result = Result.Success<string>(null);
            Assert.True(result);
            Assert.Null(result.Data);
        }

        [Fact]
        public void Typed_not_null()
        {
            var result = Result.Success("test");
            Assert.True(result);
            Assert.Equal("test", result.Data);
        }
    }
}