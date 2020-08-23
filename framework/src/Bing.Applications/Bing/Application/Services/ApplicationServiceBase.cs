using System;
using Bing.Aspects;
using Bing.Logs;
using Bing.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Application.Services
{
    /// <summary>
    /// 应用服务基类
    /// </summary>
    public abstract class ApplicationServiceBase : IApplicationService
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
        /// 当前用户
        /// </summary>
        protected ICurrentUser CurrentUser => LazyGetRequiredService(ref _currentUser);

        /// <summary>
        /// 当前用户
        /// </summary>
        private ICurrentUser _currentUser;

        /// <summary>
        /// 日志
        /// </summary>
        private ILog _log;

        /// <summary>
        /// 日志
        /// </summary>
        protected ILog Log => _log ??= GetLog();

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
