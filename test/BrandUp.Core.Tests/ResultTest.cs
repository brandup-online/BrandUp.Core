using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrandUp
{
    public class ResultTest
    {
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