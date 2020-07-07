using System;
using Bing.Reflection;

// ReSharper disable once CheckNamespace
namespace Bing.Exceptions
{
    /// <summary>
    /// 异常(<see cref="Exception"/>) 扩展
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// 解包
        /// </summary>
        /// <param name="ex">异常</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static Exception Unwrap(this Exception ex)
        {
            if(ex is null)
                throw new ArgumentNullException(nameof(ex));
            while (ex.InnerException!=null) 
                ex = ex.InnerException;
            return ex;
        }

        /// <summary>
        /// 解包
        /// </summary>
        /// <param name="ex">异常</param>
        /// <param name="untilType">截止类型</param>
        /// <param name="mayDerivedClass">是否允许派生类</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static Exception Unwrap(this Exception ex, Type untilType, bool mayDerivedClass = true)
        {
            if(ex is null)
                throw new ArgumentNullException(nameof(ex));
            if(untilType is null)
                throw new ArgumentNullException(nameof(ex));
            if (!untilType.IsSubclassOf(typeof(Exception)))
                throw new ArgumentException($"Type '{untilType}' does not devide from {typeof(Exception)}", nameof(untilType));
            if (ex.InnerException is null)
                return null;
            var exception = ex.Unwrap();
            return exception.GetType() == untilType || mayDerivedClass && exception.GetType().IsSubclassOf(untilType)
                ? exception
                : Unwrap(exception, untilType);
        }

        /// <summary>
        /// 解包
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="ex">异常</param>
        public static Exception Unwrap<TException>(this Exception ex) where TException : Exception => ex.Unwrap(Types.Of<TException>());

        /// <summary>
        /// 解包并返回消息
        /// </summary>
        /// <param name="ex">异常</param>
        public static string ToUnwrappedString(this Exception ex) => ex.Unwrap().Message;
    }
}
