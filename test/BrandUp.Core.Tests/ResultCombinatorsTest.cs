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

        [Fact]
        public async Task MapAsync_AsyncMapper()
        {
            var mapped = await Result.Success(5).MapAsync(async x =>
            {
                await Task.Yield();
                return x * 2;
            });
            Assert.Equal(10, mapped.Data);

            var chained = await Task.FromResult(Result.Success(5))
                .MapAsync(async x =>
                {
                    await Task.Yield();
                    return x + 1;
                });
            Assert.Equal(6, chained.Data);

            var failed = await Result.Error<int>("code", "message").MapAsync(x => Task.FromResult(x));
            Assert.False(failed.IsSuccess);
        }

        [Fact]
        public void Bind_FromDatalessResult()
        {
            var bound = Result.Success().Bind(() => Result.Success(42));
            Assert.Equal(42, bound.Data);

            var failed = Result.Error("code", "message").Bind(() => Result.Success(42));
            Assert.Equal("code", failed.Errors.Single().Code);

            var chainedDataless = Result.Success().Bind(() => Result.Error("inner", "failed"));
            Assert.False(chainedDataless.IsSuccess);
        }

        [Fact]
        public async Task EnsureAsync_AsyncPredicate()
        {
            var passed = await Result.Success(5).EnsureAsync(async x =>
            {
                await Task.Yield();
                return x > 0;
            }, "negative", "Must be positive.");
            Assert.True(passed.IsSuccess);

            var failed = await Task.FromResult(Result.Success(-5))
                .EnsureAsync(x => x > 0, "negative", "Must be positive.", ErrorKind.Validation);
            Assert.False(failed.IsSuccess);
            Assert.Equal(ErrorKind.Validation, failed.Errors.Single().Kind);
        }

        [Fact]
        public void Ensure_WithDescriptor()
        {
            var failed = Result.Success(-5).Ensure(x => x > 0, ExampleErrors.OrderNotFound, 42);

            Assert.False(failed.IsSuccess);
            var error = failed.Errors.Single();
            Assert.Equal(ExampleErrors.OrderNotFound.Code, error.Code);
            Assert.Equal(ErrorKind.NotFound, error.Kind);
            Assert.Equal(42, error.Arguments.Single());
        }

        [Fact]
        public async Task MatchAsync_AsyncBranches_OnTaskReceiver_Unwraps()
        {
            var value = await Task.FromResult(Result.Success(5)).MatchAsync(
                async x =>
                {
                    await Task.Yield();
                    return $"ok:{x}";
                },
                async errors =>
                {
                    await Task.Yield();
                    return "fail";
                });

            // Regression: without the async-branch overload the sync one used to win with
            // TOut = Task<string>, double-wrapping the value.
            Assert.IsType<string>((object)value);
            Assert.Equal("ok:5", value);

            var dataless = await Task.FromResult(Result.Success()).MatchAsync(() => "ok", _ => "fail");
            Assert.Equal("ok", dataless);

            var datalessAsync = await Task.FromResult(Result.Error("code", "message")).MatchAsync(
                async () =>
                {
                    await Task.Yield();
                    return "ok";
                },
                async errors =>
                {
                    await Task.Yield();
                    return "fail";
                });
            Assert.Equal("fail", datalessAsync);
        }

        [Fact]
        public async Task BindAsync_DatalessChain()
        {
            var chained = await Task.FromResult(Result.Success())
                .BindAsync(async () =>
                {
                    await Task.Yield();
                    return Result.Success();
                });
            Assert.True(chained.IsSuccess);

            var failed = await Task.FromResult(Result.Error("code", "message"))
                .BindAsync(() => Task.FromResult(Result.Success()));
            Assert.False(failed.IsSuccess);
        }

        [Fact]
        public async Task EnsureAsync_WithDescriptor_OnTaskReceiver()
        {
            var failed = await Task.FromResult(Result.Success(-5))
                .EnsureAsync(x => x > 0, ExampleErrors.OrderNotFound, 42);
            Assert.Equal(ExampleErrors.OrderNotFound.Code, failed.Errors.Single().Code);

            var asyncFailed = await Task.FromResult(Result.Success(-5))
                .EnsureAsync(async x =>
                {
                    await Task.Yield();
                    return x > 0;
                }, ExampleErrors.OrderNotFound, 42);
            Assert.Equal(ExampleErrors.OrderNotFound.Code, asyncFailed.Errors.Single().Code);
        }

        [Fact]
        public async Task MatchAsync_FoldsBothBranches()
        {
            var success = await Result.Success(5).MatchAsync(
                async x =>
                {
                    await Task.Yield();
                    return $"ok:{x}";
                },
                async errors =>
                {
                    await Task.Yield();
                    return "fail";
                });
            Assert.Equal("ok:5", success);

            var folded = await Task.FromResult(Result.Error<int>("code", "message"))
                .MatchAsync(x => "ok", errors => $"fail:{errors.Count()}");
            Assert.Equal("fail:1", folded);
        }
    }
}
