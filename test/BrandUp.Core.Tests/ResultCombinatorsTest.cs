using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BrandUp
{
    public class ResultCombinatorsTest
    {
        [Fact]
        public void Map_TransformsSuccess_PassesErrorsThrough()
        {
            Assert.Equal(6, Result.Success(5).Map(x => x + 1).Data);

            var failed = Result.Error<int>("code", "message").Map(x => x + 1);
            Assert.False(failed.IsSuccess);
            Assert.Equal("code", failed.Errors.Single().Code);
        }

        [Fact]
        public void Bind_ChainsSuccess_PassesErrorsThrough()
        {
            var chained = Result.Success(5).Bind(x => Result.Success(x.ToString()));
            Assert.Equal("5", chained.Data);

            var boundToError = Result.Success(5).Bind(_ => Result.Error<string>("inner", "failed"));
            Assert.Equal("inner", boundToError.Errors.Single().Code);

            var failed = Result.Error<int>("outer", "failed").Bind(x => Result.Success(x.ToString()));
            Assert.Equal("outer", failed.Errors.Single().Code);
        }

        [Fact]
        public void Ensure_FailsPredicate_ProducesTypedError()
        {
            var passed = Result.Success(5).Ensure(x => x > 0, "negative", "Must be positive.");
            Assert.True(passed.IsSuccess);

            var failed = Result.Success(-5).Ensure(x => x > 0, "negative", "Must be positive.", ErrorKind.Validation);
            Assert.False(failed.IsSuccess);
            var error = failed.Errors.Single();
            Assert.Equal("negative", error.Code);
            Assert.Equal(ErrorKind.Validation, error.Kind);
        }

        [Fact]
        public void Match_FoldsBothBranches()
        {
            Assert.Equal("ok:5", Result.Success(5).Match(x => $"ok:{x}", errors => "fail"));
            Assert.Equal("fail:1", Result.Error<int>("code", "message").Match(x => "ok", errors => $"fail:{errors.Count()}"));

            Assert.Equal("ok", Result.Success().Match(() => "ok", _ => "fail"));
            Assert.Equal("fail", Result.Error("code", "message").Match(() => "ok", _ => "fail"));
        }

        [Fact]
        public async Task AsyncCombinators_Chain()
        {
            var chained = await Task.FromResult(Result.Success(5))
                .MapAsync(x => x + 1)
                .BindAsync(x => Task.FromResult(Result.Success(x * 10)));

            Assert.Equal(60, chained.Data);

            var failed = await Task.FromResult(Result.Error<int>("code", "message"))
                .BindAsync(x => Task.FromResult(Result.Success(x)));
            Assert.False(failed.IsSuccess);
        }
    }
}
