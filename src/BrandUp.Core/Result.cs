using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BrandUp
{
    public class Result
    {
        readonly IReadOnlyCollection<IError> errors;

        public bool IsSuccess => errors == null;
        public IReadOnlyCollection<IError> Errors => errors;

        internal Result() { }
        internal Result(IList<IError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));
            if (errors.Count == 0)
                throw new ArgumentException("Errors required.", nameof(errors));

            this.errors = new ReadOnlyCollection<IError>(errors);
        }

        #region Success

        public static Result Success()
        {
            return new Result();
        }
        public static Result<TData> Success<TData>(TData obj)
        {
            return new Result<TData>(obj);
        }

        #endregion

        #region Error

        public static Result Error(IEnumerable<IError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));
            if (!errors.Any())
                throw new ArgumentException("Errors is empty", nameof(errors));

            return new Result(new List<IError>(errors));
        }
        public static Result<TData> Error<TData>(IEnumerable<IError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));
            if (!errors.Any())
                throw new ArgumentException("Errors is empty", nameof(errors));

            return new Result<TData>(new List<IError>(errors));
        }
        public static Result Error(string code, string message)
        {
            return new Result(new List<IError> { new Error(code, message) });
        }
        public static Result<TData> Error<TData>(string code, string message)
        {
            return new Result<TData>(new List<IError> { new Error(code, message) });
        }

        #endregion

        public static implicit operator bool(Result d) => d.IsSuccess;

        #region Object members

        public override string ToString()
        {
            if (IsSuccess)
                return "Success";
            else
                return $"Errors: {errors.Count}";
        }

        #endregion
    }

    public class Result<TData> : Result
    {
        public TData Data { get; }

        internal Result(TData obj)
        {
            Data = obj ?? throw new ArgumentNullException(nameof(obj));
        }
        internal Result(IList<IError> errors) : base(errors) { }
    }

    public class Error : IError
    {
        public string Code { get; }
        public string Message { get; }

        public Error(string code, string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Error message is required.");

            Code = code ?? string.Empty;
            Message = message;
        }
    }

    public interface IError
    {
        string Code { get; }
        string Message { get; }
    }
}