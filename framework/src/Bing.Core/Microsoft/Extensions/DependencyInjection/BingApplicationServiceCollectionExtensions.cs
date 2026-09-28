using Bing;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Bing.Internal;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 基于完整依赖图的应用注册入口。
/// </summary>
/// <remarks>
/// 该入口在服务集合中完成模块目录构建、分阶段配置和可选的自动服务注册。
/// </remarks>
public static class BingApplicationServiceCollectionExtensions
{
    /// <summary>
    /// 按依赖顺序配置应用。
    /// </summary>
    /// <typeparam name="TStartupModule">启动模块。</typeparam>
    /// <param name="services">当前应用独享的服务集合。</param>
    /// <param name="configure">仅在本次注册期间读取的配置。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>同一服务集合不可与 <see cref="ServiceCollectionApplicationExtensions.AddBing"/> 混用。</remarks>
    public static IServiceCollection AddBingApplication<TStartupModule>(this IServiceCollection services,
        Action<BingApplicationOptions> configure = null) where TStartupModule : BingModule
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        var registration = BeginRegistration(services);
        var options = new BingApplicationOptions();
        DependencyTypeFinder dependencyFinder = null;
        try
        {
            configure?.Invoke(options);
            registration.SetAutoRegisterServices(options.AutoRegisterServices);
            var registrationMode = options.ServiceRegistrationMode;
            var modules = Prepare<TStartupModule>(services, options, registration, out dependencyFinder);
            if (modules.Any(m => BingModuleHooks.RequiresAsyncConfiguration(m.Instance)))
                throw new InvalidOperationException("应用含异步服务配置模块，请使用 AddBingApplicationAsync。");
            ConfigureModulesAsync(services, modules, registrationMode, false, CancellationToken.None)
                .GetAwaiter().GetResult();
            registration.EndConfiguration();
            return services;
        }
        catch (Exception ex)
        {
            registration.FailConfiguration(ex);
            throw;
        }
        finally { CompleteDependencyScan(dependencyFinder, !registration.Failed); }
    }

    /// <summary>
    /// 异步配置当前应用的全部模块。
    /// </summary>
    /// <typeparam name="TStartupModule">启动模块。</typeparam>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="configure">本次注册的配置。</param>
    /// <param name="cancellationToken">注册取消令牌。</param>
    /// <returns>原服务集合。</returns>
    public static async Task<IServiceCollection> AddBingApplicationAsync<TStartupModule>(this IServiceCollection services,
        Action<BingApplicationOptions> configure = null, CancellationToken cancellationToken = default)
        where TStartupModule : BingModule
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        var registration = BeginRegistration(services);
        var options = new BingApplicationOptions();
        DependencyTypeFinder dependencyFinder = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            configure?.Invoke(options);
            registration.SetAutoRegisterServices(options.AutoRegisterServices);
            var registrationMode = options.ServiceRegistrationMode;
            var modules = Prepare<TStartupModule>(services, options, registration, out dependencyFinder);
            await ConfigureModulesAsync(services, modules, registrationMode, true, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            registration.EndConfiguration();
            return services;
        }
        catch (Exception error)
        {
            await registration.FailConfigurationAsync(error).ConfigureAwait(false);
            throw;
        }
        finally { CompleteDependencyScan(dependencyFinder, !registration.Failed); }
    }

    /// <summary>
    /// 占用服务集合以防止重复或并发注册。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <returns>当前服务集合的模块注册记录。</returns>
    private static BingModuleRegistration BeginRegistration(IServiceCollection services)
    {
        lock (services)
        {
            if (BingModuleRegistration.Get(services) != null)
                throw new InvalidOperationException("当前集合已经注册 Bing 应用，不能重复注册或混用新旧入口。");
            var registration = BingModuleRegistration.Register(services, false);
            registration.BeginConfiguration();
            return registration;
        }
    }

    /// <summary>
    /// 构建并验证当前应用的模块图。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="options">本次应用配置。</param>
    /// <param name="registration">本次注册记录。</param>
    /// <param name="dependencyFinder">本次注册创建的约定服务查找器。</param>
    /// <typeparam name="TStartupModule">应用启动模块类型。</typeparam>
    /// <returns>已排序的模块描述符。</returns>
    private static IReadOnlyList<BingModuleDescriptor> Prepare<TStartupModule>(IServiceCollection services,
        BingApplicationOptions options, BingModuleRegistration registration, out DependencyTypeFinder dependencyFinder)
        where TStartupModule : BingModule
    {
        dependencyFinder = null;
        if (!Enum.IsDefined(typeof(BingServiceRegistrationMode), options.ServiceRegistrationMode))
            throw new ArgumentOutOfRangeException(nameof(options.ServiceRegistrationMode));
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IBingModuleLoader));
        var loader = descriptor?.ImplementationInstance as IBingModuleLoader;
        if (descriptor != null && loader == null)
            throw new InvalidOperationException("IBingModuleLoader 在容器构建前运行，请通过 AddSingleton<IBingModuleLoader>(instance) 注册。");
        loader ??= new BingModuleLoader();
        IReadOnlyList<BingModuleDescriptor> modules = null;
        BingModuleAssemblyFinder finder = null;
        var plugins = BingPluginLoader.Load(options.PluginSources, options.PluginLoadContext, loaded =>
        {
            var roots = new[] { typeof(BingCoreModule), typeof(DependencyModule), typeof(TStartupModule) }
                .Concat(options.AdditionalModules).Concat(loaded.StartupModules).ToArray();
            modules = loader.LoadModules(roots, options.ExcludedModules)
                ?? throw new InvalidOperationException("模块加载器返回了空目录。");
            foreach (var module in modules.Where(m => m != null)) registration.Own(module.Instance);
            ValidateLoadedModules(modules, roots, options.ExcludedModules);
            finder = new BingModuleAssemblyFinder(modules, options.ServiceScanning.AdditionalAssemblies.ToArray(),
                options.PluginLoadContext);
        });
        registration.SetPlugins(plugins.Plugins);
        registration.SetModules(modules);
        services.Replace(ServiceDescriptor.Singleton<IAllAssemblyFinder>(finder));
        dependencyFinder = new DependencyTypeFinder(finder, registration.AutoRegisterServices ?
            options.ServiceScanning.ConventionalTypeFilter : null);
        services.Replace(ServiceDescriptor.Singleton<IDependencyTypeFinder>(dependencyFinder));
        services.AddCoreServices();
        services.AddCoreBingServices();
        foreach (var module in modules) services.AddSingleton(typeof(BingModule), module.Instance);
        return modules;
    }

    /// <summary>
    /// 按完整模块序列逐轮配置服务。
    /// </summary>
    private static async Task ConfigureModulesAsync(IServiceCollection services,
        IReadOnlyList<BingModuleDescriptor> modules, BingServiceRegistrationMode registrationMode,
        bool asynchronous, CancellationToken token)
    {
        foreach (var module in modules)
            await ConfigureAsync(module, services, "PreConfigureServices", "PreConfigureServices", asynchronous, token)
                .ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (registrationMode == BingServiceRegistrationMode.BeforeConfigureServices)
        {
            var dependencyModule = modules.Select(m => m.Instance).OfType<DependencyModule>().FirstOrDefault();
            if (dependencyModule == null)
                throw new InvalidOperationException("模块目录缺少 DependencyModule，无法提前注册约定服务。");
            dependencyModule.RegisterConventionServices(services);
            BingModuleRegistration.Get(services).ConventionServicesRegistered = true;
            BingLoader.RegisterTypes(services);
            token.ThrowIfCancellationRequested();
        }
        foreach (var module in modules)
            await ConfigureAsync(module, services, "ConfigureServices", "AddServices", asynchronous, token)
                .ConfigureAwait(false);
        foreach (var module in modules)
            await ConfigureAsync(module, services, "PostConfigureServices", "PostConfigureServices", asynchronous, token)
                .ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (registrationMode == BingServiceRegistrationMode.Compatible)
            BingLoader.RegisterTypes(services);
        token.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 结束本次注册的约定扫描并清除筛选器引用。
    /// </summary>
    private static void CompleteDependencyScan(DependencyTypeFinder finder, bool successful)
    {
        finder?.CompleteRegistration(successful);
    }

    /// <summary>
    /// 执行模块配置阶段并补充阶段异常信息。
    /// </summary>
    /// <param name="module">待配置的模块描述符。</param>
    /// <param name="phase">当前配置阶段名称。</param>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="diagnosticPhase">异常诊断阶段。</param>
    /// <param name="asynchronous">是否执行异步钩子。</param>
    /// <param name="token">注册取消令牌。</param>
    private static async Task ConfigureAsync(BingModuleDescriptor module, IServiceCollection services,
        string phase, string diagnosticPhase, bool asynchronous, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            await BingModuleHooks.ConfigureAsync(module.Instance, services, phase, asynchronous, token)
                .ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            ex.Data["Bing.ModuleType"] = module.ModuleType.FullName;
            ex.Data["Bing.ModulePhase"] = diagnosticPhase;
            throw;
        }
    }

    /// <summary>
    /// 验证自定义加载器返回的模块目录与依赖图一致。
    /// </summary>
    /// <param name="modules">加载器返回的模块描述符。</param>
    /// <param name="roots">根模块类型。</param>
    /// <param name="excluded">排除的模块类型。</param>
    private static void ValidateLoadedModules(IReadOnlyList<BingModuleDescriptor> modules,
        IEnumerable<Type> roots, IEnumerable<Type> excluded)
    {
        var graph = ModuleDependencyGraph.Build(roots, excluded);
        var loaded = new HashSet<Type>();
        for (var i = 0; i < modules.Count; i++)
        {
            var module = modules[i] ?? throw new InvalidOperationException("模块描述符不能为空。");
            if (!graph.Dependencies.TryGetValue(module.ModuleType, out var dependencies) ||
                !new HashSet<Type>(module.Dependencies).SetEquals(dependencies) ||
                !dependencies.All(loaded.Contains) || !loaded.Add(module.ModuleType) || module.ExecutionOrder != i)
                throw new InvalidOperationException($"加载器返回的模块图或执行顺序无效：{module.ModuleType.FullName}。");
        }
        if (!loaded.SetEquals(graph.Types)) throw new InvalidOperationException("加载器遗漏了必需模块。");
    }

}
