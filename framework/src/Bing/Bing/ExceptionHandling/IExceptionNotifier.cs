namespace Bing.ExceptionHandling;

/// <summary>
/// 异常通知器
/// </summary>
public interface IExceptionNotifier
{
    /// <summary>
    /// 通知
    /// </summary>
    /// <param name="context">异常通知上下文</param>
    Task NotifyAsync(ExceptionNotificationContext context);
}
