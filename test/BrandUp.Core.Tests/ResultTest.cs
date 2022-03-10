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
    }
}
