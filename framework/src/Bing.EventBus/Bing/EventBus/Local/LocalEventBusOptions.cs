using Bing.Collections;

namespace Bing.EventBus.Local;

/// <summary>
/// 配置本地事件总线使用的事件处理器类型。
/// </summary>
public class LocalEventBusOptions
{
    /// <summary>
    /// 获取或设置单次事件发布链允许的最大嵌套深度。
    /// </summary>
    /// <remarks>用于阻止处理器递归发布事件导致无限递归；默认值为 64。</remarks>
    public int MaxPublishDepth { get; set; } = 64;

    /// <summary>
    /// 获取本地事件处理器类型列表，默认初始化为空列表。
    /// </summary>
    public ITypeList<IEventHandler> Handlers { get; }

    /// <summary>
    /// 初始化一个 <see cref="LocalEventBusOptions" /> 类型的实例。
    /// </summary>
    /// <remarks>
    /// 初始化 <see cref="LocalEventBusOptions"/> 的实例及空处理器类型列表。
    /// </remarks>
    public LocalEventBusOptions()
    {
        Handlers = new TypeList<IEventHandler>();
    }
}
