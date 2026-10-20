using Bing.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore;

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

    /// <inheritdoc />
    public bool ResolveEnabled => _httpContextAccessor.HttpContext != null;

    /// <inheritdoc />
    public IServiceProvider ScopedProvider => _httpContextAccessor.HttpContext.RequestServices;

    /// <summary>
    /// 获取是否空请求服务
    /// </summary>
    protected bool EmptyRequestService => _httpContextAccessor.HttpContext.RequestServices == null;

    /// <summary>
    /// 初始化一个 <see cref="RequestScopedServiceResolver"/> 类型的实例。
    /// </summary>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    public RequestScopedServiceResolver(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    /// <inheritdoc />
    public T GetService<T>() => ResolveEnabled
        ? EmptyRequestService ? default : ScopedProvider.GetService<T>()
        : default;

    /// <inheritdoc />
    public object GetService(Type serviceType) => ResolveEnabled
        ? EmptyRequestService ? null : ScopedProvider.GetService(serviceType)
        : null;

    /// <inheritdoc />
    public IEnumerable<T> GetServices<T>() => ResolveEnabled
        ? EmptyRequestService ? new List<T>() : ScopedProvider.GetServices<T>()
        : new List<T>();

    /// <inheritdoc />
    public IEnumerable<object> GetServices(Type serviceType) => ResolveEnabled
        ? EmptyRequestService ? new List<object>() : ScopedProvider.GetServices(serviceType)
        : new List<object>();
}