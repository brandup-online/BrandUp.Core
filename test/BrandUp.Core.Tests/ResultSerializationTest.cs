using System.Linq;
using System.Text;
using BrandUp.Serialization;
using Xunit;

namespace BrandUp
{
    public class ResultSerializationTest
    {
        static readonly JsonResultSerializer serializer = new();

        public sealed record OrderModel(string Name, int Count);

        [Fact]
        public void RoundTrips_TypedSuccess()
        {
            var payload = serializer.Serialize(Result.Success(new OrderModel("first", 2)));

            var restored = serializer.Deserialize(payload, typeof(Result<OrderModel>));

            var typed = Assert.IsType<Result<OrderModel>>(restored);
            Assert.True(typed.IsSuccess);
            Assert.Equal(new OrderModel("first", 2), typed.Data);
        }

        [Fact]
        public void RoundTrips_DatalessSuccess()
        {
            var payload = serializer.Serialize(Result.Success());

            var restored = serializer.Deserialize(payload, typeof(Result));

            Assert.NotNull(restored);
            Assert.True(restored.IsSuccess);
        }

        [Fact]
        public void RoundTrips_ErrorsWithArguments()
        {
            var payload = serializer.Serialize(Result.Error<OrderModel>(ExampleErrors.OrderNotFound, 42));

            var restored = serializer.Deserialize(payload, typeof(Result<OrderModel>));

            var typed = Assert.IsType<Result<OrderModel>>(restored);
            Assert.False(typed.IsSuccess);
            var error = Assert.Single(typed.Errors);
            Assert.Equal(ExampleErrors.OrderNotFound.Code, error.Code);
            Assert.Equal(ErrorKind.NotFound, error.Kind);
            Assert.Equal("Order 42 not found.", error.Message);
            Assert.Equal(42L, Assert.Single(error.Arguments));
        }

        [Fact]
        public void MismatchedDataType_IsMiss()
        {
            var payload = serializer.Serialize(Result.Success(new OrderModel("first", 2)));

            Assert.Null(serializer.Deserialize(payload, typeof(Result<int>)));
        }

        [Fact]
        public void MalformedPayload_IsMiss()
        {
            Assert.Null(serializer.Deserialize(Encoding.UTF8.GetBytes("not json"), typeof(Result)));
        }
    }
}
