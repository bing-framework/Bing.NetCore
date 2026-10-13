using Bing.Collections;

namespace Bing.EventBus.Local;

/// <summary>
/// 配置本地事件总线使用的事件处理器类型。
/// </summary>
public class LocalEventBusOptions
{
    /// <summary>
    /// 获取本地事件处理器类型列表。
    /// </summary>
    /// <remarks>
    /// 默认初始化为空列表。
    /// </remarks>
    public ITypeList<IEventHandler> Handlers { get; }

    /// <summary>
    /// 初始化一个 <see cref="LocalEventBusOptions"/> 类型的实例。
    /// </summary>
    public LocalEventBusOptions()
    {
        Handlers = new TypeList<IEventHandler>();
    }
}