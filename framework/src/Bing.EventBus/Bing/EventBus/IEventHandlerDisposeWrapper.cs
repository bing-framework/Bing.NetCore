namespace Bing.EventBus;

/// <summary>
/// 包装事件处理器及其处理完成后的资源释放操作。
/// </summary>
public interface IEventHandlerDisposeWrapper : IDisposable
{
    /// <summary>
    /// 获取要执行的事件处理器。
    /// </summary>
    IEventHandler EventHandler { get; }
}