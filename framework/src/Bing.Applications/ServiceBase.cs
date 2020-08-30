using System;
using Bing.Aspects;
using Bing.Logs;
using Bing.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Applications
{
    /// <summary>
    /// 应用服务
    /// </summary>
    [Obsolete("请使用ApplicationService")]
    public abstract class ServiceBase : IService
    {
        /// <summary>
        /// 日志
        /// </summary>
        private ILog _log;

        /// <summary>
        /// 服务提供程序锁
        /// </summary>
        protected readonly object ServiceProviderLock = new object();

        /// <summary>
        /// 日志
        /// </summary>
        protected ILog Log => _log ??= GetLog();

        /// <summary>
        /// 用户会话
        /// </summary>
        protected virtual ISession Session => LazyGetRequiredService(ref _session);

        /// <summary>
        /// 用户会话
        /// </summary>
        private ISession _session;

        /// <summary>
        /// 服务定位器
        /// </summary>
        [Autowired]
        public virtual IServiceProvider ServiceProvider { get; set; }

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
        /// 获取日志操作
        /// </summary>
        protected virtual ILog GetLog()
        {
            try
            {
                return Bing.Logs.Log.GetLog(this);
            }
            catch
            {
                return Bing.Logs.Log.Null;
            }
        }
    }
}
