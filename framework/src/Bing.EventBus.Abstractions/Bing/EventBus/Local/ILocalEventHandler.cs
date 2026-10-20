namespace Bing.EventBus.Local;

/// <summary>
/// 本地事件处理器
/// </summary>
public interface ILocalEventHandler : IEventHandler
{
    /// <summary>
    /// 获取序号
    /// </summary>
    int Order { get; }
}
