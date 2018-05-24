using System;
using System.Collections.Generic;
using System.Text;
using Bing.Exceptions.Prompts;

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
        /// <returns></returns>
        public static Exception GetRawException(this Exception exception)
        {
            if (exception == null)
            {
                return null;
            }

            if (exception is Autofac.Core.DependencyResolutionException dependencyResolutionException)
            {
                return GetRawException(dependencyResolutionException.InnerException);
            }

            if (exception is AspectCore.DynamicProxy.AspectInvocationException aspectInvocationException)
            {
                return GetRawException(aspectInvocationException.InnerException);
            }

            return exception;
        }

        /// <summary>
        /// 获取异常提示
        /// </summary>
        /// <param name="exception">异常</param>
        /// <returns></returns>
        public static string GetPrompt(this Exception exception)
        {
            return ExceptionPrompt.GetPrompty(exception);
        }
    }
}
