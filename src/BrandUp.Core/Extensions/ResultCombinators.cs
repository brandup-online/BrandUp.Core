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

        /// <summary>
        /// Chains a data-less result-producing operation on success; a failed result passes
        /// through unchanged.
        /// </summary>
        /// <param name="result">Source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static Result Bind(this Result result, Func<Result> bind)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(bind);

            return result.IsSuccess ? bind() : result;
        }

        /// <summary>
        /// Chains a data-producing operation on a successful data-less result; a failed result
        /// passes its errors through.
        /// </summary>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> bind)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(bind);

            return result.IsSuccess ? bind() : Result.Error<TOut>(result.Errors);
        }

        /// <summary>
        /// Transforms the data of a successful result asynchronously; a failed result passes its
        /// errors through.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="map">Asynchronous transformation applied on success.</param>
        public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> map)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(map);

            return result.IsSuccess ? Result.Success(await map(result.Data).ConfigureAwait(false)) : Result.Error<TOut>(result.Errors);
        }

        /// <summary>
        /// Awaits the result and transforms the data of a success asynchronously.
        /// </summary>
        /// <typeparam name="TIn">Source data type.</typeparam>
        /// <typeparam name="TOut">Produced data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="map">Asynchronous transformation applied on success.</param>
        public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> map)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return await (await resultTask.ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);
        }

        /// <summary>
        /// Turns a successful result into a cataloged error when its data fails the predicate;
        /// a failed result passes through unchanged.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="predicate">Condition the data must satisfy.</param>
        /// <param name="descriptor">Descriptor of the produced error (see <see cref="ErrorCatalog"/>).</param>
        /// <param name="arguments">Format arguments of the descriptor's message template.</param>
        public static Result<TData> Ensure<TData>(this Result<TData> result, Func<TData, bool> predicate, ErrorDescriptor descriptor, params object?[] arguments)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(predicate);
            ArgumentNullException.ThrowIfNull(descriptor);

            if (!result.IsSuccess || predicate(result.Data))
                return result;

            return Result.Error<TData>(descriptor, arguments);
        }

        /// <summary>
        /// Turns a successful result into an error when its data fails the asynchronous
        /// predicate; a failed result passes through unchanged.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="predicate">Asynchronous condition the data must satisfy.</param>
        /// <param name="code">Error code used when the predicate fails.</param>
        /// <param name="message">Error message used when the predicate fails.</param>
        /// <param name="kind">Semantic category of the produced error.</param>
        public static async Task<Result<TData>> EnsureAsync<TData>(this Result<TData> result, Func<TData, Task<bool>> predicate, string code, string message, ErrorKind kind = ErrorKind.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(predicate);

            if (!result.IsSuccess || await predicate(result.Data).ConfigureAwait(false))
                return result;

            return Result.Error<TData>(code, message, kind);
        }

        /// <summary>
        /// Turns a successful result into a cataloged error when its data fails the asynchronous
        /// predicate; a failed result passes through unchanged.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="predicate">Asynchronous condition the data must satisfy.</param>
        /// <param name="descriptor">Descriptor of the produced error (see <see cref="ErrorCatalog"/>).</param>
        /// <param name="arguments">Format arguments of the descriptor's message template.</param>
        public static async Task<Result<TData>> EnsureAsync<TData>(this Result<TData> result, Func<TData, Task<bool>> predicate, ErrorDescriptor descriptor, params object?[] arguments)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(predicate);
            ArgumentNullException.ThrowIfNull(descriptor);

            if (!result.IsSuccess || await predicate(result.Data).ConfigureAwait(false))
                return result;

            return Result.Error<TData>(descriptor, arguments);
        }

        /// <summary>
        /// Awaits the result and applies
        /// <see cref="Ensure{TData}(Result{TData}, Func{TData, bool}, string, string, ErrorKind)"/>.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="predicate">Condition the data must satisfy.</param>
        /// <param name="code">Error code used when the predicate fails.</param>
        /// <param name="message">Error message used when the predicate fails.</param>
        /// <param name="kind">Semantic category of the produced error.</param>
        public static async Task<Result<TData>> EnsureAsync<TData>(this Task<Result<TData>> resultTask, Func<TData, bool> predicate, string code, string message, ErrorKind kind = ErrorKind.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return (await resultTask.ConfigureAwait(false)).Ensure(predicate, code, message, kind);
        }

        /// <summary>
        /// Awaits the result and applies the asynchronous-predicate
        /// <see cref="EnsureAsync{TData}(Result{TData}, Func{TData, Task{bool}}, string, string, ErrorKind)"/>.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="predicate">Asynchronous condition the data must satisfy.</param>
        /// <param name="code">Error code used when the predicate fails.</param>
        /// <param name="message">Error message used when the predicate fails.</param>
        /// <param name="kind">Semantic category of the produced error.</param>
        public static async Task<Result<TData>> EnsureAsync<TData>(this Task<Result<TData>> resultTask, Func<TData, Task<bool>> predicate, string code, string message, ErrorKind kind = ErrorKind.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return await (await resultTask.ConfigureAwait(false)).EnsureAsync(predicate, code, message, kind).ConfigureAwait(false);
        }

        /// <summary>
        /// Folds the result into a single value with asynchronous branches.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="onSuccess">Branch applied to the data on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static async Task<TOut> MatchAsync<TData, TOut>(this Result<TData> result, Func<TData, Task<TOut>> onSuccess, Func<IEnumerable<IError>, Task<TOut>> onError)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onError);

            return result.IsSuccess ? await onSuccess(result.Data).ConfigureAwait(false) : await onError(result.Errors).ConfigureAwait(false);
        }

        /// <summary>
        /// Folds a data-less result into a single value with asynchronous branches.
        /// </summary>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="result">Source result.</param>
        /// <param name="onSuccess">Branch applied on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static async Task<TOut> MatchAsync<TOut>(this Result result, Func<Task<TOut>> onSuccess, Func<IEnumerable<IError>, Task<TOut>> onError)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onError);

            return result.IsSuccess ? await onSuccess().ConfigureAwait(false) : await onError(result.Errors).ConfigureAwait(false);
        }

        /// <summary>
        /// Awaits the result and applies
        /// <see cref="Match{TData, TOut}(Result{TData}, Func{TData, TOut}, Func{IEnumerable{IError}, TOut})"/>.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="onSuccess">Branch applied to the data on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static async Task<TOut> MatchAsync<TData, TOut>(this Task<Result<TData>> resultTask, Func<TData, TOut> onSuccess, Func<IEnumerable<IError>, TOut> onError)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return (await resultTask.ConfigureAwait(false)).Match(onSuccess, onError);
        }

        /// <summary>
        /// Awaits the result and folds it with asynchronous branches.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="onSuccess">Branch applied to the data on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static async Task<TOut> MatchAsync<TData, TOut>(this Task<Result<TData>> resultTask, Func<TData, Task<TOut>> onSuccess, Func<IEnumerable<IError>, Task<TOut>> onError)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return await (await resultTask.ConfigureAwait(false)).MatchAsync(onSuccess, onError).ConfigureAwait(false);
        }

        /// <summary>
        /// Awaits the data-less result and applies
        /// <see cref="Match{TOut}(Result, Func{TOut}, Func{IEnumerable{IError}, TOut})"/>.
        /// </summary>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="onSuccess">Branch applied on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<TOut> onSuccess, Func<IEnumerable<IError>, TOut> onError)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return (await resultTask.ConfigureAwait(false)).Match(onSuccess, onError);
        }

        /// <summary>
        /// Awaits the data-less result and folds it with asynchronous branches.
        /// </summary>
        /// <typeparam name="TOut">Produced value type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="onSuccess">Branch applied on success.</param>
        /// <param name="onError">Branch applied to the errors on failure.</param>
        public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<Task<TOut>> onSuccess, Func<IEnumerable<IError>, Task<TOut>> onError)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return await (await resultTask.ConfigureAwait(false)).MatchAsync(onSuccess, onError).ConfigureAwait(false);
        }

        /// <summary>
        /// Chains an asynchronous data-less operation on a successful data-less result; a failed
        /// result passes through unchanged.
        /// </summary>
        /// <param name="result">Source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static async Task<Result> BindAsync(this Result result, Func<Task<Result>> bind)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(bind);

            return result.IsSuccess ? await bind().ConfigureAwait(false) : result;
        }

        /// <summary>
        /// Awaits the data-less result and chains an asynchronous data-less operation on success.
        /// </summary>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="bind">Operation applied on success.</param>
        public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Task<Result>> bind)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return await (await resultTask.ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);
        }

        /// <summary>
        /// Awaits the result and applies the cataloged-error
        /// <see cref="Ensure{TData}(Result{TData}, Func{TData, bool}, ErrorDescriptor, object?[])"/>.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="predicate">Condition the data must satisfy.</param>
        /// <param name="descriptor">Descriptor of the produced error (see <see cref="ErrorCatalog"/>).</param>
        /// <param name="arguments">Format arguments of the descriptor's message template.</param>
        public static async Task<Result<TData>> EnsureAsync<TData>(this Task<Result<TData>> resultTask, Func<TData, bool> predicate, ErrorDescriptor descriptor, params object?[] arguments)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return (await resultTask.ConfigureAwait(false)).Ensure(predicate, descriptor, arguments);
        }

        /// <summary>
        /// Awaits the result and applies the asynchronous-predicate cataloged-error
        /// <see cref="EnsureAsync{TData}(Result{TData}, Func{TData, Task{bool}}, ErrorDescriptor, object?[])"/>.
        /// </summary>
        /// <typeparam name="TData">Data type.</typeparam>
        /// <param name="resultTask">Task producing the source result.</param>
        /// <param name="predicate">Asynchronous condition the data must satisfy.</param>
        /// <param name="descriptor">Descriptor of the produced error (see <see cref="ErrorCatalog"/>).</param>
        /// <param name="arguments">Format arguments of the descriptor's message template.</param>
        public static async Task<Result<TData>> EnsureAsync<TData>(this Task<Result<TData>> resultTask, Func<TData, Task<bool>> predicate, ErrorDescriptor descriptor, params object?[] arguments)
        {
            ArgumentNullException.ThrowIfNull(resultTask);

            return await (await resultTask.ConfigureAwait(false)).EnsureAsync(predicate, descriptor, arguments).ConfigureAwait(false);
        }
    }
}
