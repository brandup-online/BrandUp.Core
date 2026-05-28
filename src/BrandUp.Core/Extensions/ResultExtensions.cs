namespace BrandUp
{
    /// <summary>
    /// Extensions for converting between <see cref="Result"/> and <see cref="Result{TData}"/>.
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>
        /// Reinterprets a failed result as a typed <see cref="Result{TData}"/>, preserving its errors.
        /// </summary>
        /// <typeparam name="TData">Target data type.</typeparam>
        /// <param name="result">A failed result.</param>
        /// <returns>A failed <see cref="Result{TData}"/> with the same errors.</returns>
        /// <exception cref="ArgumentException"><paramref name="result"/> is successful.</exception>
        public static Result<TData> AsObjectiveErrors<TData>(this Result result)
        {
            if (result.IsSuccess)
                throw new ArgumentException("Result required is errors.");

            return new Result<TData>([.. result.Errors]);
        }
    }
}