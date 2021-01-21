using System;
using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Logs;
using Bing.Logs.Core;
using Bing.Sessions;

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
        protected DomainServiceBase() => Log = NullLog.Instance;

        /// <summary>
        /// 日志
        /// </summary>
        protected ILog Log { get; set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        [Obsolete("领域服务不该使用用户会话")]
        protected virtual ISession Session => LazyServiceProvider.LazyGetRequiredService<ISession>();
    }
}
