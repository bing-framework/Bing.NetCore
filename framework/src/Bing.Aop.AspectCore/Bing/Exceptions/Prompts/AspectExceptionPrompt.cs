namespace Bing.Exceptions.Prompts;

/// <summary>
/// AOP 异常提示
/// </summary>
public class AspectExceptionPrompt : IExceptionPrompt
{
    /// <summary>
    /// 获取异常提示
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>异常提示文本；不适用或异常为空时返回空值或空字符串。</returns>
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
    /// <returns>解除 AOP 包装后的原始异常。</returns>
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
