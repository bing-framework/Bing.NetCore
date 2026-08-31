namespace Bing.ExceptionHandling;

/// <summary>
/// 不执行任何异常通知的空对象实现。
/// </summary>
public class NullExceptionNotifier : IExceptionNotifier
{
    /// <summary>
    /// 获取可全局复用的无状态空通知器实例。
    /// </summary>
    public static IExceptionNotifier Instance { get; } = new NullExceptionNotifier();

    /// <summary>
    /// 初始化 <see cref="NullExceptionNotifier"/> 的实例。
    /// </summary>
    private NullExceptionNotifier()
    {
    }

    /// <inheritdoc />
    /// <remarks>立即完成且不读取或修改 <paramref name="context"/>。</remarks>
    public Task NotifyAsync(ExceptionNotificationContext context) => Task.CompletedTask;
}
