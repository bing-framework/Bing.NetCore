using System;
using Bing.Exceptions;
using Bing.Exceptions.Prompts;

// ReSharper disable once CheckNamespace
namespace Bing
{
    /// <summary>
    /// 异常扩展
    /// </summary>
    public static partial class ExceptionExtensions
    {
        /// <summary>
        /// 获取原始异常
        /// </summary>
        /// <param name="exception">异常</param>
        public static Exception GetRawException(this Exception exception)
        {
            if (exception is null)
                return null;
            if (exception is AspectCore.DynamicProxy.AspectInvocationException aspectInvocationException)
            {
                return aspectInvocationException.InnerException is null
                    ? aspectInvocationException
                    : GetRawException(aspectInvocationException.InnerException);
            }
            return exception;
        }

        /// <summary>
        /// 获取异常提示
        /// </summary>
        /// <param name="exception">异常</param>
        /// <param name="isProduction">是否生产环境</param>
        public static string GetPrompt(this Exception exception, bool isProduction = false) => ExceptionPrompt.GetPrompt(exception, isProduction);

        /// <summary>
        /// 获取Http状态码
        /// </summary>
        /// <param name="exception">异常</param>
        public static int GetHttpStatusCode(this Exception exception)
        {
            if (exception is null)
                return 200;
            exception = exception.GetRawException();
            if (exception is Warning warning)
                return warning.HttpStatusCode;
            return 200;
        }

        /// <summary>
        /// 获取错误码
        /// </summary>
        /// <param name="exception">异常</param>
        public static string GetErrorCode(this Exception exception)
        {
            if (exception is null)
                return null;
            exception = exception.GetRawException();
            if (exception is Warning warning)
                return warning.Code;
            return null;
        }
    }
}
