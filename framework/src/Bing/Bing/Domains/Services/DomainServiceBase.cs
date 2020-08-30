using System;
using Bing.Aspects;
using Bing.Logs;
using Bing.Logs.Core;
using Bing.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Domains.Services
{
    /// <summary>
    /// 领域服务抽象基类
    /// </summary>
    public abstract class DomainServiceBase : IDomainService
    {
        /// <summary>
        /// 服务定位器
        /// </summary>
        [Autowired]
        public virtual IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 服务提供程序锁
        /// </summary>
        protected readonly object ServiceProviderLock = new object();

        /// <summary>
        /// 懒加载获取请求服务
        /// </summary>
        /// <typeparam name="TService">服务类型</typeparam>
        /// <param name="reference">服务引用</param>
        protected TService LazyGetRequiredService<TService>(ref TService reference) => LazyGetRequiredService(typeof(TService), ref reference);

        /// <summary>
        /// 懒加载获取请求服务
        /// </summary>
        /// <typeparam name="TRef">引用类型</typeparam>
        /// <param name="serviceType">服务类型</param>
        /// <param name="reference">服务引用</param>
        protected TRef LazyGetRequiredService<TRef>(Type serviceType, ref TRef reference)
        {
            if (reference == null)
            {
                lock (ServiceProviderLock)
                {
                    if (reference == null)
                        reference = (TRef)ServiceProvider.GetRequiredService(serviceType);
                }
            }
            return reference;
        }

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
        protected virtual ISession Session => LazyGetRequiredService(ref _session);

        /// <summary>
        /// 用户会话
        /// </summary>
        private ISession _session;
    }
}
