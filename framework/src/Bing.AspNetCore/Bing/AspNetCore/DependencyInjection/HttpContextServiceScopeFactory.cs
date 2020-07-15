using System;
using Bing.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.DependencyInjection
{
    /// <summary>
    /// 基于当前HttpContext的<see cref="IServiceScope"/>工厂。如果当前操作处于HttpRequest作用域中，直接使用HttpRequest的作用域，否则创建新的作用域
    /// </summary>
    [Dependency(ServiceLifetime.Singleton, ReplaceExisting = true)]
    public class HttpContextServiceScopeFactory : IHybridServiceScopeFactory
    {
        /// <summary>
        /// 服务作用域工厂
        /// </summary>
        public IServiceScopeFactory ServiceScopeFactory { get; }

        /// <summary>
        /// 当前请求的<see cref="IHttpContextAccessor"/>对象
        /// </summary>
        protected IHttpContextAccessor HttpContextAccessor { get; }

        /// <summary>
        /// 初始化一个<see cref="HttpContextServiceScopeFactory"/>类型的实例
        /// </summary>
        /// <param name="serviceScopeFactory">服务作用域工厂</param>
        /// <param name="httpContextAccessor">Http上下文访问器</param>
        public HttpContextServiceScopeFactory(IServiceScopeFactory serviceScopeFactory
            , IHttpContextAccessor httpContextAccessor)
        {
            ServiceScopeFactory = serviceScopeFactory;
            HttpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 创建依赖注入服务的作用域。如果当前操作处于HttpRequest作用域中，直接使用HttpReqeust的作用域，否则创建新的作用域
        /// </summary>
        public virtual IServiceScope CreateScope()
        {
            var httpContext = HttpContextAccessor?.HttpContext;
            // 不在HttpRequest作用域中
            if (httpContext == null)
                return ServiceScopeFactory.CreateScope();
            return new NonDisposedHttpContextServiceScope(httpContext.RequestServices);
        }

        /// <summary>
        /// 当前HttpRequest的<see cref="IServiceScope"/>的包装，保持HttpContext.RequestServices的可传递性，并且不释放
        /// </summary>
        protected class NonDisposedHttpContextServiceScope : IServiceScope
        {
            /// <summary>
            /// 服务提供程序
            /// </summary>
            public IServiceProvider ServiceProvider { get; }

            /// <summary>
            /// 初始化一个<see cref="NonDisposedHttpContextServiceScope"/>类型的实例
            /// </summary>
            /// <param name="serviceProvider">服务提供程序</param>
            public NonDisposedHttpContextServiceScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

            /// <summary>
            /// 释放资源。因为是HttpContext的，啥也不做，避免在using使用时被释放
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
