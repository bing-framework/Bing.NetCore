namespace Bing.ExceptionHandling;

/// <summary>
/// 为异常订阅器提供默认处理顺序的基类。
/// </summary>
public abstract class ExceptionSubscriber : IExceptionSubscriber
{
    /// <inheritdoc />
    /// <remarks>默认顺序为 <c>10</c>。</remarks>
    public virtual int Order => 10;

    /// <inheritdoc />
    public abstract Task HandleAsync(ExceptionNotificationContext context);
}
