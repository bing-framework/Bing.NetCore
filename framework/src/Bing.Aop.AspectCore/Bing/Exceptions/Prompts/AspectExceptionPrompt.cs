namespace Bing.Exceptions.Prompts;

/// <summary>
/// AOP 异常提示
/// </summary>
public class AspectExceptionPrompt : IExceptionPrompt
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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
