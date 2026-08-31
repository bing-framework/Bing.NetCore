namespace Bing.ExceptionHandling;

/// <summary>
/// 定义异步分发异常通知的能力。
/// </summary>
public interface IExceptionNotifier
{
    /// <summary>
    /// 异步通知异常订阅器。
    /// </summary>
    /// <param name="context">包含异常、日志级别和处理状态的通知上下文。</param>
    Task NotifyAsync(ExceptionNotificationContext context);
}
