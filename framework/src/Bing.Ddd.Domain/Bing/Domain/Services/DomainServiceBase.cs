using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Logs;
using Bing.Logs.Core;

namespace Bing.Domain.Services
{
    /// <summary>
    /// 领域服务抽象基类
    /// </summary>
    public abstract class DomainServiceBase : IDomainService
    {
        /// <summary>
        /// Lazy延迟加载服务提供程序
        /// </summary>
        [Autowired]
        public virtual ILazyServiceProvider LazyServiceProvider { get; set; }

        /// <summary>
        /// 初始化一个<see cref="DomainServiceBase"/>类型的实例
        /// </summary>
        protected DomainServiceBase() { }

        /// <summary>
        /// 日志
        /// </summary>
        protected ILog Log => LazyServiceProvider.LazyGetService<ILog>() ?? NullLog.Instance;
    }
}
