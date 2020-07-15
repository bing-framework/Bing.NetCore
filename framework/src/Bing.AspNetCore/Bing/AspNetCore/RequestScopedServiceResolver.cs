using System;
using System.Collections.Generic;
using Bing.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore
{
    /// <summary>
    /// Request的<see cref="ServiceLifetime.Scoped"/>服务解析器
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, TryAdd = true)]
    public class RequestScopedServiceResolver : IScopedServiceResolver
    {
        /// <summary>
        /// Http上下文访问器
        /// </summary>
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// 是否可解析
        /// </summary>
        public bool ResolveEnabled => _httpContextAccessor.HttpContext != null;

        /// <summary>
        /// <see cref="ServiceLifetime.Scoped"/>生命周期的服务提供程序
        /// </summary>
        public IServiceProvider ScopedProvider => _httpContextAccessor.HttpContext.RequestServices;

        /// <summary>
        /// 是否空请求服务
        /// </summary>
        protected bool EmptyRequestService => _httpContextAccessor.HttpContext.RequestServices == null;

        /// <summary>
        /// 初始化一个<see cref="RequestScopedServiceResolver"/>类型的实例
        /// </summary>
        /// <param name="httpContextAccessor">Http上下文访问器</param>
        public RequestScopedServiceResolver(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

        /// <summary>
        /// 获取指定服务类型的实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public T GetService<T>() => ResolveEnabled
            ? EmptyRequestService ? default : ScopedProvider.GetService<T>()
            : default;

        /// <summary>
        /// 获取指定服务类型的实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public object GetService(Type serviceType) => ResolveEnabled
            ? EmptyRequestService ? null : ScopedProvider.GetService(serviceType)
            : null;

        /// <summary>
        /// 获取指定服务类型的所有实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public IEnumerable<T> GetServices<T>() => ResolveEnabled
            ? EmptyRequestService ? new List<T>() : ScopedProvider.GetServices<T>()
            : new List<T>();

        /// <summary>
        /// 获取指定服务类型的所有实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public IEnumerable<object> GetServices(Type serviceType) => ResolveEnabled
            ? EmptyRequestService ? new List<object>() : ScopedProvider.GetServices(serviceType)
            : new List<object>();
    }
}
