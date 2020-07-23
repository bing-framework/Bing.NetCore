using System;
using Bing.DependencyInjection;
using Bing.Logs;
using Bing.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Applications
{
    /// <summary>
    /// 应用服务
    /// </summary>
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
        public ILog Log => _log ??= GetLog();

        /// <summary>
        /// 用户会话
        /// </summary>
        public virtual ISession Session => Bing.Sessions.Session.Instance;

        /// <summary>
        /// 服务定位器
        /// </summary>
        public virtual IServiceProvider ServiceProvider => ServiceLocator.Instance.ScopedProvider;

        /// <summary>
        /// 懒加载获取请求服务
        /// </summary>
        /// <typeparam name="TService">服务类型</typeparam>
        /// <param name="reference">服务引用</param>
        protected TService LazyGetRequiredService<TService>(ref TService reference)
        {
            if (reference == null)
            {
                lock (ServiceProviderLock)
                {
                    if (reference == null)
                        reference = ServiceProvider.GetRequiredService<TService>();
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
