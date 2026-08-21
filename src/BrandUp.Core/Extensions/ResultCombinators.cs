namespace BrandUp
{
    /// <summary>
    /// Functional combinators over <see cref="Result"/>/<see cref="Result{TData}"/> for chaining
    /// operations without explicit <c>IsSuccess</c> ladders. Errors of a failed result flow
    /// through unchanged.
    /// </summary>
    public static class ResultCombinators
    {
        /// <summary>
        /// Transforms the data of a successful result; a failed result passes its errors through.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="map">Transformation applied on success.</param>
        public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(map);

            return result.IsSuccess ? Result.Success(map(result.Data)) : Result.Error<TOut>(result.Errors);
        }

        /// <summary>
        /// Chains a result-producing operation on the data of a successful result; a failed
        /// result passes its errors through.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(bind);

            return result.IsSuccess ? bind(result.Data) : Result.Error<TOut>(result.Errors);
        }

        /// <summary>
        /// Chains a data-less operation on the data of a successful result; a failed result
        /// passes its errors through.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> bind)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(bind);

            return result.IsSuccess ? bind(result.Data) : Result.Error(result.Errors);
        }

        /// <summary>
        /// Turns a successful result into an error when its data fails the predicate; a failed
        /// result passes through unchanged.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="predicate">Condition the data must satisfy.</param>
        /// <param name="code">Error code used when the predicate fails.</param>
        /// <param name="message">Error message used when the predicate fails.</param>
        /// <param name="kind">Semantic category of the produced error.</param>
        public static Result<TData> Ensure<TData>(this Result<TData> result, Func<TData, bool> predicate, string code, string message, ErrorKind kind = ErrorKind.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(predicate);

            if (!result.IsSuccess || predicate(result.Data))
                return result;

            return Result.Error<TData>(code, message, kind);
        }

        /// <summary>
        /// Folds the result into a single value: one branch for success, one for errors.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="onSuccess">Branch applied to the data on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static TOut Match<TData, TOut>(this Result<TData> result, Func<TData, TOut> onSuccess, Func<IEnumerable<IError>, TOut> onError)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onError);

            return result.IsSuccess ? onSuccess(result.Data) : onError(result.Errors);
        }

        /// <summary>
        /// Folds a data-less result into a single value: one branch for success, one for errors.
        /// </summary>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="onSuccess">Branch applied on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<IEnumerable<IError>, TOut> onError)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onError);

            return result.IsSuccess ? onSuccess() : onError(result.Errors);
        }

        /// <summary>
        /// Awaits the result and applies <see cref="Map{TIn, TOut}(Result{TIn}, Func{TIn, TOut})"/>.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="map">Transformation applied on success.</param>
        public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> map)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return (await resultTask.ConfigureAwait(false)).Map(map);
        }

        /// <summary>
        /// Awaits the result and chains an asynchronous result-producing operation on success.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static async Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result<TOut>>> bind)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return await (await resultTask.ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);
        }

        /// <summary>
        /// Chains an asynchronous result-producing operation on the data of a successful result.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static async Task<Result<TOut>> BindAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<Result<TOut>>> bind)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(bind);

            return result.IsSuccess ? await bind(result.Data).ConfigureAwait(false) : Result.Error<TOut>(result.Errors);
        }

        /// <summary>
        /// Awaits the result and chains an asynchronous data-less operation on success.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static async Task<Result> BindAsync<TIn>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result>> bind)
        {
            ArgumentNullException.ThrowIfNull(resultTask);
            ArgumentNullException.ThrowIfNull(bind);

            var result = await resultTask.ConfigureAwait(false);

            return result.IsSuccess ? await bind(result.Data).ConfigureAwait(false) : Result.Error(result.Errors);
        }
    }
}
