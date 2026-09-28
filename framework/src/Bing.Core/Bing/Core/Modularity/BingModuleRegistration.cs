using Bing.Core.Builders;
using Bing.DependencyInjection;
using Bing.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bing.Core.Modularity;

/// <summary>
/// 保存注册期与运行期共用的应用所有权记录。
/// </summary>
/// <remarks>
/// 记录不使用进程级模块缓存，模块实例只归当前应用所有。
/// </remarks>
internal sealed class BingModuleRegistration : IBingModuleContainer
{
    /// <summary>
    /// 框架创建并拥有的模块实例。
    /// </summary>
    private readonly List<BingModule> _owned = new();

    /// <summary>
    /// 已释放模块实例的引用集合。
    /// </summary>
    private readonly HashSet<BingModule> _released = new(ModuleReferenceComparer.Instance);

    /// <summary>
    /// 标记注册记录是否已经完成终态释放。
    /// </summary>
    private bool _runtimeReleased;

    /// <summary>
    /// 模块是否正在配置服务。
    /// </summary>
    private bool _configuring;

    /// <summary>
    /// 已附加的应用根服务提供程序。
    /// </summary>
    private IServiceProvider _provider;

    /// <summary>
    /// 最近绑定的旧入口配置对象。
    /// </summary>
    private IConfiguration _legacyConfiguration;

    /// <summary>
    /// 保护注册记录的附加操作。
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// 当前应用的模块描述符集合。
    /// </summary>
    private IReadOnlyList<BingModuleDescriptor> _modules = Array.Empty<BingModuleDescriptor>();

    /// <summary>
    /// 当前应用的插件描述符容器。
    /// </summary>
    private readonly BingPluginContainer _pluginContainer = new();

    /// <summary>
    /// 初始化模块注册记录。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="legacy">是否使用旧兼容注册入口。</param>
    /// <param name="autoRegisterServices">是否启用约定服务自动注册。</param>
    internal BingModuleRegistration(IServiceCollection services, bool legacy, bool autoRegisterServices = true)
    {
        Services = services;
        Legacy = legacy;
        AutoRegisterServices = autoRegisterServices;
    }

    /// <summary>
    /// 获取应用服务集合。
    /// </summary>
    internal IServiceCollection Services { get; }

    /// <summary>
    /// 获取是否使用旧兼容注册入口。
    /// </summary>
    internal bool Legacy { get; }

    /// <summary>
    /// 获取是否启用约定服务自动注册。
    /// </summary>
    internal bool AutoRegisterServices { get; private set; }

    /// <summary>
    /// 约定服务是否已在主配置前注册。
    /// </summary>
    internal bool ConventionServicesRegistered { get; set; }

    /// <summary>
    /// 获取模块配置是否已经失败。
    /// </summary>
    internal bool Failed { get; private set; }

    /// <summary>
    /// 获取模块运行时是否已附加根服务提供程序。
    /// </summary>
    internal bool IsAttached => _provider != null;

    /// <inheritdoc />
    public IReadOnlyList<BingModuleDescriptor> Modules => _modules;

    /// <summary>
    /// 从服务集合获取模块注册记录。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>已注册的模块记录；未找到时返回 <c>null</c>。</returns>
    internal static BingModuleRegistration Get(IServiceCollection services) =>
        services.FirstOrDefault(d => d.ServiceType == typeof(BingModuleRegistration))?.ImplementationInstance as BingModuleRegistration;

    /// <summary>
    /// 注册或复用模块注册记录。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="legacy">是否使用旧兼容注册入口。</param>
    /// <param name="autoRegisterServices">是否启用约定服务自动注册。</param>
    /// <returns>当前服务集合所属的模块注册记录。</returns>
    internal static BingModuleRegistration Register(IServiceCollection services, bool legacy, bool autoRegisterServices = true)
    {
        var existing = Get(services);
        if (existing != null)
        {
            if (existing.Legacy != legacy)
                throw new InvalidOperationException("同一服务集合不能混用 AddBing 与 AddBingApplication。");
            existing.EnsureConfigurable();
            existing.BindLegacyRegistration();
            return existing;
        }
        var registration = new BingModuleRegistration(services, legacy, autoRegisterServices);
        registration.BindLegacyRegistration();
        services.AddSingleton(registration);
        services.AddSingleton<IBingModuleContainer>(registration);
        // 工厂创建的 manager 由容器追踪释放；模块实例本身由 registration 单独管理。
        services.AddSingleton<IBingModuleManager>(provider => new BingModuleManager(provider, registration));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BingModuleHostedService>());
        // 插件容器使用独立实例，并在生命周期服务之后注册，避免扩展别名改变根容器的释放图。
        services.AddSingleton<IBingPluginContainer>(registration._pluginContainer);
        return registration;
    }

    /// <summary>
    /// 验证当前注册记录仍允许追加模块配置。
    /// </summary>
    internal void EnsureConfigurable()
    {
        if (_configuring)
            throw new InvalidOperationException("模块配置仍在进行，不能重入或重复注册应用。");
        if (Failed)
            throw new InvalidOperationException("模块配置已失败，请丢弃当前服务集合并重新创建应用。");
        if (_provider != null || _runtimeReleased)
            throw new InvalidOperationException("模块运行时已创建，不能继续注册模块。");
    }

    /// <summary>
    /// 占用当前服务集合直到全部配置阶段完成。
    /// </summary>
    internal void BeginConfiguration()
    {
        lock (_gate)
        {
            EnsureConfigurable();
            _configuring = true;
        }
    }

    /// <summary>
    /// 结束当前服务集合的配置过程。
    /// </summary>
    internal void EndConfiguration()
    {
        lock (_gate) _configuring = false;
    }

    /// <summary>
    /// 设置本次注册是否启用约定 DI。
    /// </summary>
    internal void SetAutoRegisterServices(bool value) => AutoRegisterServices = value;

    /// <summary>
    /// 验证当前集合可用于构建 Bing 服务容器。
    /// </summary>
    internal void EnsureBuildable()
    {
        lock (_gate)
        {
            if (_configuring)
                throw new InvalidOperationException("模块配置尚未结束，不能构建 Bing 服务容器。");
            if (Failed)
                throw new InvalidOperationException("模块配置已失败，不能构建 Bing 服务容器。");
        }
    }

    /// <summary>
    /// 将模块注册记录附加到应用根服务提供程序。
    /// </summary>
    /// <param name="provider">应用根服务提供程序。</param>
    internal void Attach(IServiceProvider provider)
    {
        lock (_gate)
        {
            if (_configuring)
                throw new InvalidOperationException("模块配置尚未结束，不能启动应用。");
            if (Failed)
                throw new InvalidOperationException("模块配置已失败，不能启动应用。");
            if (_runtimeReleased)
                throw new InvalidOperationException("模块注册记录已释放，不能重新附加服务提供程序。");
            if (_provider != null && !ReferenceEquals(_provider, provider))
                throw new InvalidOperationException("模块实例不能由多个根容器共享，请为每个应用创建独立服务集合。");
            _provider = provider;
        }
    }

    /// <summary>
    /// 记录由框架创建并负责释放的模块实例。
    /// </summary>
    /// <param name="module">需要登记所有权的模块实例。</param>
    internal void Own(BingModule module)
    {
        if (!_owned.Contains(module, ModuleReferenceComparer.Instance))
            _owned.Add(module);
    }

    /// <summary>
    /// 冻结当前应用的模块描述符集合。
    /// </summary>
    /// <param name="modules">按执行顺序排列的模块描述符。</param>
    internal void SetModules(IEnumerable<BingModuleDescriptor> modules) => _modules = Array.AsReadOnly(modules.ToArray());

    /// <summary>
    /// 冻结当前应用的插件描述符集合。
    /// </summary>
    /// <param name="plugins">按插件依赖顺序排列的插件描述符。</param>
    internal void SetPlugins(IEnumerable<BingPluginDescriptor> plugins) => _pluginContainer.SetPlugins(plugins);

    /// <summary>
    /// 绑定旧兼容入口使用的静态宿主引用。
    /// </summary>
    /// <param name="provider">应用根服务提供程序。</param>
    internal void BindLegacy(IServiceProvider provider)
    {
        if (!Legacy) return;
        ServiceLocator.Instance.Bind(Services, provider);
        _legacyConfiguration = Services.GetConfigurationOrNull();
        Singleton<IConfiguration>.Instance = _legacyConfiguration;
    }

    /// <summary>
    /// 绑定旧入口注册阶段使用的静态兼容引用。
    /// </summary>
    internal void BindLegacyRegistration()
    {
        if (!Legacy) return;
        ServiceLocator.Instance.BindServices(Services);
        _legacyConfiguration = Services.GetConfigurationOrNull();
        Singleton<IConfiguration>.Instance = _legacyConfiguration;
    }

    /// <summary>
    /// 按所属者解绑旧兼容入口的静态宿主引用。
    /// </summary>
    internal void UnbindLegacy()
    {
        if (!Legacy) return;
        // 仅释放当前所属者的兼容入口；不能清除随后启动的另一个宿主。
        var configuration = _legacyConfiguration;
        try
        {
            var unbound = ServiceLocator.Instance.Unbind(Services, _provider);
            if (!unbound)
                unbound = ServiceLocator.Instance.UnbindServices(Services);
            if (unbound && ReferenceEquals(Singleton<IConfiguration>.Instance, configuration))
                Singleton<IConfiguration>.Instance = null;
        }
        finally
        {
            _legacyConfiguration = null;
        }
    }

    /// <summary>
    /// 获取是否存在只能异步释放的未释放模块。
    /// </summary>
    internal bool RequiresAsyncDisposal => _owned.Any(m => m is IAsyncDisposable && m is not IDisposable && !_released.Contains(m));

    /// <summary>
    /// 按逆序释放已拥有的模块实例。
    /// </summary>
    /// <param name="asynchronous">是否优先使用异步释放。</param>
    /// <returns>释放过程中收集的异常。</returns>
    internal async Task<List<Exception>> ReleaseAsync(bool asynchronous)
    {
        var errors = new List<Exception>();
        List<BingModule> ordered;
        lock (_gate)
        {
            ordered = _modules.Select(m => m.Instance).Reverse().Concat(_owned.AsEnumerable().Reverse())
                .Where(m => m != null)
                .Distinct(ModuleReferenceComparer.Instance)
                .ToList();
            ordered = ordered.Where(m => _released.Add(m)).ToList();
        }
        foreach (var module in ordered)
        {
            try
            {
                if (asynchronous && module is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else if (module is IDisposable disposable)
                    disposable.Dispose();
                else if (module is IAsyncDisposable asyncOnlyDisposable)
                {
                    // 配置失败发生在同步注册阶段，必须等待异步清理完成；调用方仍会收到原始配置异常。
                    BingModuleDisposal.DisposeSynchronously(asyncOnlyDisposable);
                }
            }
            catch (Exception ex) { errors.Add(ex); }
        }
        try { UnbindLegacy(); }
        catch (Exception ex) { errors.Add(ex); }
        finally
        {
            foreach (var builder in Services.Select(d => d.ImplementationInstance)
                         .OfType<BingBuilder>().Distinct())
            {
                try { builder.ClearModuleReferences(); }
                catch (Exception ex) { errors.Add(ex); }
            }
            lock (_gate)
            {
                _provider = null;
                foreach (var descriptor in _modules)
                    descriptor.ClearInstance();
                RemoveOwnedModuleDescriptors(ordered);
                _pluginContainer.Clear();
                _owned.Clear();
                _released.Clear();
                _runtimeReleased = true;
            }
        }
        return errors;
    }

    /// <summary>
    /// 移除服务集合中直接引用已拥有模块的服务描述符。
    /// </summary>
    /// <param name="modules">已经进入释放流程的模块实例。</param>
    private void RemoveOwnedModuleDescriptors(IEnumerable<BingModule> modules)
    {
        var owned = new HashSet<BingModule>(modules, ModuleReferenceComparer.Instance);
        for (var index = Services.Count - 1; index >= 0; index--)
        {
            if (Services[index].ImplementationInstance is BingModule module && owned.Contains(module))
                Services.RemoveAt(index);
        }
    }

    /// <summary>
    /// 标记配置失败并释放已经创建的模块。
    /// </summary>
    /// <param name="error">导致配置失败的原始异常。</param>
    internal void FailConfiguration(Exception error)
    {
        Failed = true;
        EndConfiguration();
        // 同步注册失败也必须等待仅异步资源释放完成，避免原始异常返回后资源仍在后台释放。
        var errors = ReleaseAsync(false).GetAwaiter().GetResult();
        if (errors.Count > 0) error.Data["Bing.ModuleCleanupErrors"] = new AggregateException(errors);
        var logger = Services.Where(d => d.ServiceType == typeof(Bing.Logging.StartupLogger))
            .Select(d => d.ImplementationInstance).OfType<Bing.Logging.StartupLogger>().FirstOrDefault();
        logger?.LogInfos.Clear();
    }

    /// <summary>
    /// 异步标记配置失败并释放已创建模块。
    /// </summary>
    internal async Task FailConfigurationAsync(Exception error)
    {
        Failed = true;
        EndConfiguration();
        var errors = await ReleaseAsync(true).ConfigureAwait(false);
        if (errors.Count > 0) error.Data["Bing.ModuleCleanupErrors"] = new AggregateException(errors);
        var logger = Services.Where(d => d.ServiceType == typeof(Bing.Logging.StartupLogger))
            .Select(d => d.ImplementationInstance).OfType<Bing.Logging.StartupLogger>().FirstOrDefault();
        logger?.LogInfos.Clear();
    }
}

/// <summary>
/// 将宿主停止通知转发给模块生命周期管理器。
/// </summary>
/// <remarks>
/// Web 管道装配仍由调用方在 <c>UseBing</c> 或 <c>UseBingAsync</c> 时完成。
/// </remarks>
internal sealed class BingModuleHostedService : IHostedService
{
    /// <summary>
    /// 模块生命周期管理器。
    /// </summary>
    private readonly IBingModuleManager _manager;

    /// <summary>
    /// 初始化宿主停止适配器。
    /// </summary>
    /// <param name="manager">模块生命周期管理器。</param>
    public BingModuleHostedService(IBingModuleManager manager) => _manager = manager;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _manager.ShutdownAsync(cancellationToken);
}
