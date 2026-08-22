namespace BrandUp.Serialization
{
    /// <summary>
    /// Serializes <see cref="Result"/>/<see cref="Result{TData}"/> instances for stores that keep
    /// them outside the process — a distributed query cache, a distributed idempotency store. The
    /// default is <see cref="JsonResultSerializer"/>.
    /// </summary>
    public interface IResultSerializer
    {
        /// <summary>
        /// Serializes the result (its success data or its errors, including
        /// <see cref="IError.Arguments"/>).
        /// </summary>
        /// <param name="result">Result to serialize.</param>
        byte[] Serialize(Result result);

        /// <summary>
        /// Rebuilds a result of the expected type from a serialized payload;
        /// <see langword="null"/> when the payload is malformed or does not match
        /// <paramref name="resultType"/> — callers treat that as a miss.
        /// </summary>
        /// <param name="payload">Serialized payload.</param>
        /// <param name="resultType">The exact <see cref="Result"/>-derived type expected
        /// (e.g. <c>Result&lt;OrderModel&gt;</c>).</param>
        Result? Deserialize(byte[] payload, Type resultType);
    }
}
