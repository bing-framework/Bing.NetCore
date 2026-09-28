using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 提供旧兼容入口使用的应用服务定位。
/// </summary>
/// <remarks>
/// 可解析单例和瞬时服务；存在有效 HTTP 作用域时也可解析作用域服务，但不负责管理根容器创建的独立作用域。
/// </remarks>
public sealed class ServiceLocator : Disposable
{
    #region 字段

    /// <summary>
    /// 旧兼容入口的单例定位器。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<ServiceLocator> InstanceLazy = new Lazy<ServiceLocator>(() => new ServiceLocator());

    /// <summary>
    /// 当前兼容入口绑定的服务提供程序。
    /// </summary>
    private IServiceProvider _provider;

    /// <summary>
    /// 当前兼容入口绑定的服务集合。
    /// </summary>
    private IServiceCollection _services;

    /// <summary>
    /// 保护静态宿主绑定状态的同步锁。
    /// </summary>
    private readonly object _bindingGate = new();

    /// <summary>
    /// 尝试以一次快照读取当前宿主绑定。
    /// </summary>
    /// <param name="services">当前服务集合。</param>
    /// <param name="provider">当前服务提供程序。</param>
    /// <returns>绑定完整时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private bool TryGetBinding(out IServiceCollection services, out IServiceProvider provider)
    {
        lock (_bindingGate)
        {
            services = _services;
            provider = _provider;
            return services != null && provider != null;
        }
    }

    /// <summary>
    /// 绑定当前兼容宿主。
    /// </summary>
    /// <remarks>新模块运行时不使用此静态入口。</remarks>
    /// <param name="services">所属服务集合。</param>
    /// <param name="provider">所属服务提供程序。</param>
    internal void Bind(IServiceCollection services, IServiceProvider provider)
    {
        lock (_bindingGate)
        {
            _services = services;
            _provider = provider;
        }
    }

    /// <summary>
    /// 绑定旧入口注册阶段使用的服务集合。
    /// </summary>
    /// <param name="services">当前服务集合。</param>
    internal void BindServices(IServiceCollection services)
    {
        Check.NotNull(services, nameof(services));
        lock (_bindingGate)
        {
            // 旧入口只支持单宿主语义；注册新服务集合时不能继续保留旧宿主的 provider。
            if (!ReferenceEquals(_services, services))
                _provider = null;
            _services = services;
        }
    }

    /// <summary>
    /// 仅解绑注册阶段绑定的服务集合。
    /// </summary>
    /// <param name="services">需要解绑的服务集合。</param>
    /// <returns>当前绑定属于指定集合且尚未附加服务提供程序时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal bool UnbindServices(IServiceCollection services)
    {
        lock (_bindingGate)
        {
            if (!ReferenceEquals(_services, services) || _provider != null)
                return false;
            _services = null;
            return true;
        }
    }

    /// <summary>
    /// 仅解绑指定所属者。
    /// </summary>
    /// <remarks>不会影响之后启动的宿主。</remarks>
    /// <param name="services">需要解绑的服务集合。</param>
    /// <param name="provider">需要解绑的服务提供程序。</param>
    /// <returns>当前绑定属于指定集合和提供程序时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal bool Unbind(IServiceCollection services, IServiceProvider provider)
    {
        lock (_bindingGate)
        {
            if (!ReferenceEquals(_services, services) || !ReferenceEquals(_provider, provider)) return false;
            _services = null;
            _provider = null;
            return true;
        }
    }

    #endregion

    #region 属性

    /// <summary>
    /// 获取服务定位器实例。
    /// </summary>
    public static ServiceLocator Instance => InstanceLazy.Value;

    /// <summary>
    /// 获取当前是否已绑定服务提供程序。
    /// </summary>
    public bool IsProviderEnabled
    {
        get
        {
            return TryGetBinding(out _, out _);
        }
    }

    /// <summary>
    /// 获取当前作用域服务提供程序。
    /// </summary>
    public IServiceProvider ScopedProvider
    {
        get
        {
            if (!TryGetBinding(out _, out var provider))
                return null;

            var scopeResolver = provider.GetService<IScopedServiceResolver>();
            return scopeResolver != null && scopeResolver.ResolveEnabled ? scopeResolver.ScopedProvider : null;
        }
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化服务定位器。
    /// </summary>
    private ServiceLocator() { }

    #endregion

    #region InScoped(是否在作用域生命周期中)

    /// <summary>
    /// 判断当前是否处于作用域生命周期中。
    /// </summary>
    /// <returns>当前存在可用的作用域服务提供程序时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool InScoped() => Instance.ScopedProvider != null;

    #endregion

    #region SetServiceCollection(设置应用程序服务集合)

    /// <summary>
    /// 设置应用程序服务集合。
    /// </summary>
    /// <param name="services">服务集合。</param>
    internal void SetServiceCollection(IServiceCollection services)
    {
        Check.NotNull(services, nameof(services));
        lock (_bindingGate)
        {
            if (!ReferenceEquals(_services, services))
                _provider = null;
            _services = services;
        }
    }

    #endregion

    #region SetApplicationServiceProvider(设置应用程序服务提供程序)

    /// <summary>
    /// 设置应用程序服务提供程序。
    /// </summary>
    /// <param name="provider">服务提供程序。</param>
    internal void SetApplicationServiceProvider(IServiceProvider provider)
    {
        Check.NotNull(provider, nameof(provider));
        lock (_bindingGate)
            _provider = provider;
    }

    #endregion

    #region GetServiceDescriptors(获取所有已注册的 ServiceDescriptor 对象)

    /// <summary>
    /// 获取所有已注册的 <see cref="ServiceDescriptor"/> 对象。
    /// </summary>
    /// <returns>当前应用程序服务集合中的服务描述序列；未绑定宿主时返回空序列。</returns>
    public IEnumerable<ServiceDescriptor> GetServiceDescriptors()
    {
        lock (_bindingGate)
            return _services ?? (IEnumerable<ServiceDescriptor>)Array.Empty<ServiceDescriptor>();
    }

    #endregion

    #region GetService(解析指定类型的服务实例)

    /// <summary>
    /// 解析指定类型的服务实例。
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>解析到的服务实例；服务提供程序不可用或未注册时返回默认值。</returns>
    public T GetService<T>()
    {
        if (!TryGetBinding(out _, out var provider))
            return default;

        var scopedResolver = provider.GetService<IScopedServiceResolver>();
        if (scopedResolver != null && scopedResolver.ResolveEnabled)
            return scopedResolver.GetService<T>();
        return provider.GetService<T>();
    }

    /// <summary>
    /// 解析指定类型的服务实例。
    /// </summary>
    /// <param name="serviceType">服务类型。</param>
    /// <returns>解析到的服务实例；服务提供程序不可用或未注册时返回 <see langword="null"/>。</returns>
    public object GetService(Type serviceType)
    {
        if (!TryGetBinding(out _, out var provider))
            return null;

        var scopedResolver = provider.GetService<IScopedServiceResolver>();
        if (scopedResolver != null && scopedResolver.ResolveEnabled)
            return scopedResolver.GetService(serviceType);
        return provider.GetService(serviceType);
    }

    #endregion

    #region GetServices(解析指定类型的所有服务实例)

    /// <summary>
    /// 解析指定类型的所有服务实例。
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>按当前作用域或根服务提供程序解析出的全部服务实例；未绑定宿主时返回空序列。</returns>
    public IEnumerable<T> GetServices<T>()
    {
        if (!TryGetBinding(out _, out var provider))
            return Array.Empty<T>();

        var scopedResolver = provider.GetService<IScopedServiceResolver>();
        if (scopedResolver != null && scopedResolver.ResolveEnabled)
            return scopedResolver.GetServices<T>();
        return provider.GetServices<T>();
    }

    /// <summary>
    /// 解析指定类型的所有服务实例。
    /// </summary>
    /// <param name="serviceType">服务类型。</param>
    /// <returns>按当前作用域或根服务提供程序解析出的全部服务实例；未绑定宿主时返回空序列。</returns>
    public IEnumerable<object> GetServices(Type serviceType)
    {
        if (!TryGetBinding(out _, out var provider))
            return Array.Empty<object>();

        var scopedResolver = provider.GetService<IScopedServiceResolver>();
        if (scopedResolver != null && scopedResolver.ResolveEnabled)
            return scopedResolver.GetServices(serviceType);
        return provider.GetServices(serviceType);
    }

    #endregion

    #region Dispose(释放资源)

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        lock (_bindingGate)
        {
            _services = null;
            _provider = null;
        }
        base.Dispose(disposing);
    }

    #endregion
}
