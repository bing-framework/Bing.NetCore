using System;
using System.Collections.Generic;
using Bing.Dependency;
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
        /// 初始化一个<see cref="RequestScopedServiceResolver"/>类型的实例
        /// </summary>
        /// <param name="httpContextAccessor">Http上下文访问器</param>
        public RequestScopedServiceResolver(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

        /// <summary>
        /// 获取指定服务类型的实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public T GetService<T>() => _httpContextAccessor.HttpContext.RequestServices.GetService<T>();

        /// <summary>
        /// 获取指定服务类型的实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public object GetService(Type serviceType) => _httpContextAccessor.HttpContext.RequestServices.GetService(serviceType);

        /// <summary>
        /// 获取指定服务类型的所有实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public IEnumerable<T> GetServices<T>() => _httpContextAccessor.HttpContext.RequestServices.GetServices<T>();

        /// <summary>
        /// 获取指定服务类型的所有实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public IEnumerable<object> GetServices(Type serviceType) => _httpContextAccessor.HttpContext.RequestServices.GetServices(serviceType);
    }
}
