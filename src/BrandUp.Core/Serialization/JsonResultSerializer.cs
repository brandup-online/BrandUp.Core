using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace BrandUp.Serialization
{
    /// <summary>
    /// System.Text.Json-based <see cref="IResultSerializer"/>. Success data is serialized with the
    /// declared data type of the result; errors keep their code, message, kind and format
    /// arguments. Arguments round-trip as JSON primitives (string, number, boolean,
    /// <see langword="null"/>) — a complex argument object degrades to its JSON text, so error
    /// templates meant for out-of-process results should format primitive arguments only.
    /// </summary>
    public sealed class JsonResultSerializer(JsonSerializerOptions? options = null) : IResultSerializer
    {
        static readonly ConcurrentDictionary<Type, Func<Result, object?>> dataGetters = new();
        static readonly ConcurrentDictionary<Type, Func<object?, Result>> successFactories = new();
        static readonly ConcurrentDictionary<Type, Func<IEnumerable<IError>, Result>> errorFactories = new();

        readonly JsonSerializerOptions options = options ?? JsonSerializerOptions.Web;

        /// <inheritdoc/>
        public byte[] Serialize(Result result)
        {
            ArgumentNullException.ThrowIfNull(result);

            var resultType = result.GetType();
            JsonElement? data = null;
            ErrorEnvelope[]? errors = null;

            if (result.IsSuccess)
            {
                if (IsGenericResult(resultType))
                {
                    var value = dataGetters.GetOrAdd(resultType, BuildDataGetter)(result);
                    if (value != null)
                        data = JsonSerializer.SerializeToElement(value, resultType.GetGenericArguments()[0], options);
                }
            }
            else
            {
                errors = [.. result.Errors.Select(error => new ErrorEnvelope(
                    error.Code,
                    error.Message,
                    error.Kind,
                    error.Arguments.Count == 0
                        ? null
                        : [.. error.Arguments.Select(argument => JsonSerializer.SerializeToElement(argument, argument?.GetType() ?? typeof(object), options))]))];
            }

            return JsonSerializer.SerializeToUtf8Bytes(new ResultEnvelope(result.IsSuccess, data, errors), options);
        }

        /// <inheritdoc/>
        public Result? Deserialize(byte[] payload, Type resultType)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(resultType);

            var isGeneric = IsGenericResult(resultType);
            if (!isGeneric && resultType != typeof(Result))
                return null;

            try
            {
                var envelope = JsonSerializer.Deserialize<ResultEnvelope>(payload, options);
                if (envelope == null)
                    return null;

                if (envelope.Success)
                {
                    if (!isGeneric)
                        return Result.Success();

                    var dataType = resultType.GetGenericArguments()[0];
                    var data = envelope.Data is { } element ? element.Deserialize(dataType, options) : null;
                    if (data == null && dataType.IsValueType && Nullable.GetUnderlyingType(dataType) == null)
                        data = Activator.CreateInstance(dataType);

                    return successFactories.GetOrAdd(dataType, BuildSuccessFactory)(data);
                }

                if (envelope.Errors is not { Length: > 0 })
                    return null;

                var errors = envelope.Errors.Select(IError (error) => new Error(
                    error.Code,
                    error.Message,
                    error.Kind,
                    error.Arguments == null ? [] : [.. error.Arguments.Select(ConvertArgument)]));

                return isGeneric
                    ? errorFactories.GetOrAdd(resultType.GetGenericArguments()[0], BuildErrorFactory)(errors)
                    : Result.Error(errors);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException)
            {
                // A malformed, tampered or shape-mismatched payload is a miss, not a dispatch
                // failure (ArgumentException covers Error reconstruction from bad data).
                return null;
            }
        }

        static bool IsGenericResult(Type resultType)
        {
            return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>);
        }

        static object? ConvertArgument(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            // Cast keeps the branches distinct: a long/double conditional would widen every
            // integer to double.
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : (object)element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };

        static Func<Result, object?> BuildDataGetter(Type resultType)
        {
            var property = resultType.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)!;
            var resultParameter = Expression.Parameter(typeof(Result));
            var access = Expression.Convert(Expression.Property(Expression.Convert(resultParameter, resultType), property), typeof(object));

            return Expression.Lambda<Func<Result, object?>>(access, resultParameter).Compile();
        }

        static Func<object?, Result> BuildSuccessFactory(Type dataType)
        {
            var method = typeof(Result).GetMethod(nameof(Result.Success), 1, [Type.MakeGenericMethodParameter(0)])!.MakeGenericMethod(dataType);
            var dataParameter = Expression.Parameter(typeof(object));
            var call = Expression.Call(method, Expression.Convert(dataParameter, dataType));

            return Expression.Lambda<Func<object?, Result>>(call, dataParameter).Compile();
        }

        static Func<IEnumerable<IError>, Result> BuildErrorFactory(Type dataType)
        {
            var method = typeof(Result).GetMethod(nameof(Result.Error), 1, [typeof(IEnumerable<IError>)])!.MakeGenericMethod(dataType);
            var errorsParameter = Expression.Parameter(typeof(IEnumerable<IError>));
            var call = Expression.Call(method, errorsParameter);

            return Expression.Lambda<Func<IEnumerable<IError>, Result>>(call, errorsParameter).Compile();
        }

        sealed record ResultEnvelope(bool Success, JsonElement? Data, ErrorEnvelope[]? Errors);

        sealed record ErrorEnvelope(string Code, string Message, ErrorKind Kind, JsonElement[]? Arguments);
    }
}
