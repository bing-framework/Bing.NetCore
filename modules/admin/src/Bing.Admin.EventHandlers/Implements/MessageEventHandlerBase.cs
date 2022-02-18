using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Logs;
using Bing.Logs.Core;

namespace Bing.Admin.EventHandlers.Implements
{
    /// <summary>
    /// 消息事件处理器基类
    /// </summary>
    public abstract class MessageEventHandlerBase : DotNetCore.CAP.ICapSubscribe
    {
        /// <summary>
        /// Lazy延迟加载服务提供程序
        /// </summary>
        [Autowired]
        public virtual ILazyServiceProvider LazyServiceProvider { get; set; }

        /// <summary>
        /// 日志
        /// </summary>
        protected ILog Log => LazyServiceProvider.LazyGetService<ILog>() ?? NullLog.Instance;
    }
}
