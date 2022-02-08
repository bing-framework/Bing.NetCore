using Bing.Collections;

namespace Bing.EventBus.Local
{
    /// <summary>
    /// 本地事件总线 选项配置
    /// </summary>
    public class LocalEventBusOptions
    {
        /// <summary>
        /// 事件处理器类型列表
        /// </summary>
        public ITypeList<IEventHandler> Handlers { get; }

        /// <summary>
        /// 初始化一个<see cref="LocalEventBusOptions"/>类型的实例
        /// </summary>
        public LocalEventBusOptions()
        {
            Handlers = new TypeList<IEventHandler>();
        }
    }
}
