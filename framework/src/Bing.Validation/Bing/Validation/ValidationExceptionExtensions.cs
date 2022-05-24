using System;
using Bing.Reflection;

namespace Bing.Validation
{
    /// <summary>
    /// 验证异常扩展
    /// </summary>
    public static class ValidationExceptionExtensions
    {
        /// <summary>
        /// 转换异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="resultCollection">验证结果集合</param>
        /// <param name="appendAction">追加操作</param>
        public static TException ToException<TException>(this Validations.ValidationResultCollection resultCollection,
            Action<TException, Validations.ValidationResultCollection> appendAction = null) where TException : BingException, new()
        {
            if (resultCollection == null)
                throw new ArgumentNullException(nameof(resultCollection));
            if (typeof(TException) == typeof(ValidationException))
                return ToException(resultCollection, (e, c) => appendAction?.Invoke(e as TException, c)) as TException;
            var exception = CreateBasicException<TException>(resultCollection);
            appendAction?.Invoke(exception, resultCollection);
            return exception;
        }

        /// <summary>
        /// 转换为异常
        /// </summary>
        /// <param name="resultCollection">验证结果集合</param>
        /// <param name="appendAction">追加操作</param>
        public static ValidationException ToException(this Validations.ValidationResultCollection resultCollection,
            Action<ValidationException, Validations.ValidationResultCollection> appendAction = null)
        {
            if (resultCollection == null)
                throw new ArgumentNullException(nameof(resultCollection));
            var exception = CreateValidationException(resultCollection);
            appendAction?.Invoke(exception, resultCollection);
            return exception;
        }

        /// <summary>
        /// 转换为异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="result">验证结果</param>
        /// <param name="appendAction">追加操作</param>
        public static TException ToException<TException>(this Validations.Abstractions.IValidationResult result,
            Action<TException, Validations.ValidationResultCollection> appendAction = null) where TException : BingException, new()
        {
            switch (result)
            {
                case Validations.ValidationResultCollection fluentResult:
                    return ToException(fluentResult, appendAction);
                case null:
                    throw new ArgumentNullException(nameof(result));
                default:
                    throw new ArgumentException($"{nameof(Validations.ValidationResultCollection)} 的类型无效");
            }
        }

        /// <summary>
        /// 转换为异常
        /// </summary>
        /// <param name="result">验证结果</param>
        /// <param name="appendAction">追加操作</param>
        public static ValidationException ToException(this Validations.Abstractions.IValidationResult result,
            Action<ValidationException, Validations.ValidationResultCollection> appendAction = null)
        {
            switch (result)
            {
                case Validations.ValidationResultCollection fluentResult:
                    return ToException(fluentResult, appendAction);

                case null:
                    throw new ArgumentNullException(nameof(result));
                default:
                    throw new ArgumentException($"{nameof(Validations.ValidationResultCollection)} 的类型无效");
            }
        }

        /// <summary>
        /// 创建基础异常
        /// </summary>
        /// <param name="result">验证结果</param>
        private static TException CreateBasicException<TException>(Validations.Abstractions.IValidationResult result) where TException : BingException, new()
        {
            var exception = Types.CreateInstance<TException>(result.ErrorCode, result.ToMessage(), result.Flag);
            exception.ExtraData.Add("ValidationResultCollection", result);
            return exception;
        }

        /// <summary>
        /// 创建验证异常
        /// </summary>
        /// <param name="result">验证结果</param>
        private static ValidationException CreateValidationException(Validations.Abstractions.IValidationResult result)
        {
            var exception = new ValidationException(result.ErrorCode, result.ToMessage(), result.ToValidationMessages(),
                result.Flag);
            exception.ExtraData.Add("ValidationResultCollection", result);
            return exception;
        }

        /// <summary>
        /// 引发异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="resultCollection">验证结果集合</param>
        /// <param name="appendAction">追加操作</param>
        public static void RaiseException<TException>(this Validations.ValidationResultCollection resultCollection,
            Action<TException, Validations.ValidationResultCollection> appendAction = null) where TException : BingException, new() =>
            throw resultCollection.ToException(appendAction);

        /// <summary>
        /// 引发异常
        /// </summary>
        /// <param name="resultCollection">验证结果集合</param>
        /// <param name="appendAction">追加操作</param>
        public static void RaiseException(this Validations.ValidationResultCollection resultCollection,
            Action<ValidationException, Validations.ValidationResultCollection> appendAction = null) =>
            throw resultCollection.ToException(appendAction);
    }
}
