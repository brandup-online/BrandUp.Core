using System;
using System.Collections.Generic;

namespace BrandUp
{
    public static class ResultExtensions
    {
        public static Result<TData> AsObjectiveErrors<TData>(this Result result)
        {
            if (result.IsSuccess)
                throw new ArgumentException("Result required is errors.");

            return new Result<TData>(new List<IError>(result.Errors));
        }
    }
}