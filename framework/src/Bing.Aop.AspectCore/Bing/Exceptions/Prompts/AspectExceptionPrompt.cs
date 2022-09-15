using System;

namespace Bing.Exceptions.Prompts
{
    /// <summary>
    /// AOP 异常提示
    /// </summary>
    public class AspectExceptionPrompt : IExceptionPrompt
    {
        /// <summary>
        /// 获取异常提示
        /// </summary>
        /// <param name="exception">异常</param>
        public string GetPrompt(Exception exception)
        {
            if (exception == null)
                return null;
            if (exception is AspectCore.DynamicProxy.AspectInvocationException aspectInvocationException)
            {
                return aspectInvocationException.InnerException is null
                    ? aspectInvocationException.Message
                    : GetRawException(aspectInvocationException.InnerException).Message;
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取原始异常
        /// </summary>
        /// <param name="exception">异常</param>
        public Exception GetRawException(Exception exception)
        {
            if (exception is AspectCore.DynamicProxy.AspectInvocationException aspectInvocationException)
            {
                if (aspectInvocationException.InnerException == null)
                    return aspectInvocationException;
                return GetRawException(aspectInvocationException.InnerException);
            }
            return exception;
        }
    }
}
