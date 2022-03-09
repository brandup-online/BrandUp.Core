using System;
using System.Collections.Generic;

namespace BrandUp
{
    public static class ResultExtensions
    {
        public static IResult<TData> AsObjectiveErrors<TData>(this IResult result)
        {
            if (result.IsSuccess)
                throw new ArgumentException("Result required is errors.");

            return new Result<TData>(new List<IError>(result.Errors));
        }
    }
}