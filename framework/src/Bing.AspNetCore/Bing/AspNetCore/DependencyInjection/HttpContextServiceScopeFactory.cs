using Bing.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.DependencyInjection;

/// <summary>
/// 基于当前HttpContext的<see cref="IServiceScope"/>工厂。
/// </summary>
/// <remarks>
/// 如果当前操作处于HttpRequest作用域中，直接使用HttpRequest的作用域，否则创建新的作用域
/// </remarks>
[Dependency(ServiceLifetime.Singleton, ReplaceExisting = true)]
public class HttpContextServiceScopeFactory : IHybridServiceScopeFactory
{
    /// <summary>
    /// 获取服务作用域工厂
    /// </summary>
    public IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 获取当前请求的对象
    /// </summary>
    protected IHttpContextAccessor HttpContextAccessor { get; }

    /// <summary>
    /// 初始化一个 <see cref="HttpContextServiceScopeFactory"/> 类型的实例。
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    public HttpContextServiceScopeFactory(IServiceScopeFactory serviceScopeFactory
        , IHttpContextAccessor httpContextAccessor)
    {
        ServiceScopeFactory = serviceScopeFactory;
        HttpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 如果当前操作处于HttpRequest作用域中，直接使用HttpReqeust的作用域，否则创建新的作用域
    /// </remarks>
    public virtual IServiceScope CreateScope()
    {
        var httpContext = HttpContextAccessor?.HttpContext;
        // 不在HttpRequest作用域中
        if (httpContext == null)
            return ServiceScopeFactory.CreateScope();
        return new NonDisposedHttpContextServiceScope(httpContext.RequestServices);
    }

    /// <summary>
    /// 包装当前请求的服务作用域。
    /// </summary>
    /// <remarks>保留 <see cref="HttpContext.RequestServices"/> 的传递性，且不释放该请求服务提供程序。</remarks>
    protected class NonDisposedHttpContextServiceScope : IServiceScope
    {
        /// <inheritdoc />
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 初始化一个 <see cref="NonDisposedHttpContextServiceScope"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public NonDisposedHttpContextServiceScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;

        /// <inheritdoc />
        /// <remarks>
        /// 释放资源。因为是HttpContext的，啥也不做，避免在using使用时被释放
        /// </remarks>
        public void Dispose()
        {
        }
    }
}
