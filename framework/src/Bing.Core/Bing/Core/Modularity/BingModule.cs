using Bing.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 为 Bing 模块提供默认生命周期行为的基类。
/// </summary>
public abstract class BingModule : IBingModule, IPreConfigureServices, IPostConfigureServices,
    IOnPreApplicationInitialization, IOnPostApplicationInitialization, IBingModuleShutdown
{
    /// <summary>
    /// 前置配置接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingPreConfiguration;

    /// <summary>
    /// 后置配置接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingPostConfiguration;

    /// <summary>
    /// 异步前置配置接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingAsyncPreConfiguration;

    /// <summary>
    /// 异步后置配置接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingAsyncPostConfiguration;

    /// <summary>
    /// 前置初始化接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingPreInitialization;

    /// <summary>
    /// 后置初始化接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingPostInitialization;

    /// <summary>
    /// 异步前置初始化接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingAsyncPreInitialization;

    /// <summary>
    /// 异步后置初始化接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingAsyncPostInitialization;

    /// <summary>
    /// 异步初始化旧接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingAsyncInitialization;

    /// <summary>
    /// 同步关闭旧接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingShutdown;

    /// <summary>
    /// 异步关闭旧接口桥接是否正在执行。
    /// </summary>
    private bool _bridgingAsyncShutdown;

    /// <inheritdoc />
    public virtual ModuleLevel Level => ModuleLevel.Business;

    /// <inheritdoc />
    public virtual int Order => 0;

    /// <inheritdoc />
    public virtual bool Enabled { get; protected set; }

    /// <inheritdoc />
    /// <remarks>默认实现不注册额外服务，直接返回 <paramref name="services"/>。</remarks>
    public virtual IServiceCollection AddServices(IServiceCollection services) => services;

    /// <summary>
    /// 在所有模块注册服务前配置当前模块。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    public virtual void PreConfigureServices(IServiceCollection services)
    {
        if (_bridgingPreConfiguration) return;
        _bridgingPreConfiguration = true;
        try
        {
            if (this is IPreConfigureServices pre &&
                BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IPreConfigureServices),
                    nameof(IPreConfigureServices.PreConfigureServices), typeof(IServiceCollection)))
                pre.PreConfigureServices(services);
            else if (this is IBingPreConfigureServices legacy &&
                     BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IBingPreConfigureServices),
                         nameof(IBingPreConfigureServices.PreConfigureServices), typeof(IServiceCollection)))
                legacy.PreConfigureServices(services);
        }
        finally { _bridgingPreConfiguration = false; }
    }

    /// <summary>
    /// 在所有模块注册服务前配置当前模块。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public virtual async Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
    {
        if (_bridgingAsyncPreConfiguration) return;
        _bridgingAsyncPreConfiguration = true;
        try
        {
            if (BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IPreConfigureServices),
                    nameof(IPreConfigureServices.PreConfigureServicesAsync), typeof(IServiceCollection),
                    typeof(CancellationToken)))
                await ((IPreConfigureServices)this).PreConfigureServicesAsync(services, cancellationToken)
                    .ConfigureAwait(false);
            else PreConfigureServices(services);
        }
        finally { _bridgingAsyncPreConfiguration = false; }
    }

    /// <summary>
    /// 配置当前模块的服务。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    public virtual void ConfigureServices(IServiceCollection services) => AddServices(services);

    /// <summary>
    /// 配置当前模块的服务。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public virtual Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
    {
        ConfigureServices(services);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 在所有模块注册服务后配置当前模块。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    public virtual void PostConfigureServices(IServiceCollection services)
    {
        if (_bridgingPostConfiguration) return;
        _bridgingPostConfiguration = true;
        try
        {
            if (this is IPostConfigureServices post &&
                BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IPostConfigureServices),
                    nameof(IPostConfigureServices.PostConfigureServices), typeof(IServiceCollection)))
                post.PostConfigureServices(services);
            else if (this is IBingPostConfigureServices legacy &&
                     BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IBingPostConfigureServices),
                         nameof(IBingPostConfigureServices.PostConfigureServices), typeof(IServiceCollection)))
                legacy.PostConfigureServices(services);
        }
        finally { _bridgingPostConfiguration = false; }
    }

    /// <summary>
    /// 在所有模块注册服务后配置当前模块。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public virtual async Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
    {
        if (_bridgingAsyncPostConfiguration) return;
        _bridgingAsyncPostConfiguration = true;
        try
        {
            if (BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IPostConfigureServices),
                    nameof(IPostConfigureServices.PostConfigureServicesAsync), typeof(IServiceCollection),
                    typeof(CancellationToken)))
                await ((IPostConfigureServices)this).PostConfigureServicesAsync(services, cancellationToken)
                    .ConfigureAwait(false);
            else PostConfigureServices(services);
        }
        finally { _bridgingAsyncPostConfiguration = false; }
    }

    /// <inheritdoc />
    /// <remarks>默认实现仅将 <see cref="Enabled"/> 标记为 <c>true</c>。</remarks>
    public virtual void UseModule(IServiceProvider provider) => Enabled = true;

    /// <summary>
    /// 在应用主初始化前初始化当前模块。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    public virtual void OnPreApplicationInitialization(BingModuleInitializationContext context)
    {
        if (_bridgingPreInitialization || this is not IOnPreApplicationInitialization initializer ||
            !BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IOnPreApplicationInitialization),
                nameof(IOnPreApplicationInitialization.OnPreApplicationInitialization),
                typeof(BingModuleInitializationContext))) return;
        _bridgingPreInitialization = true;
        try { initializer.OnPreApplicationInitialization(context); }
        finally { _bridgingPreInitialization = false; }
    }

    /// <summary>
    /// 在应用主初始化前初始化当前模块。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public virtual async Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
        CancellationToken cancellationToken)
    {
        if (_bridgingAsyncPreInitialization) return;
        _bridgingAsyncPreInitialization = true;
        try
        {
            if (BingModuleHooks.HasIndependentInterfaceImplementation(this,
                    typeof(IOnPreApplicationInitialization),
                    nameof(IOnPreApplicationInitialization.OnPreApplicationInitializationAsync),
                    typeof(BingModuleInitializationContext), typeof(CancellationToken)))
                await ((IOnPreApplicationInitialization)this)
                    .OnPreApplicationInitializationAsync(context, cancellationToken).ConfigureAwait(false);
            else OnPreApplicationInitialization(context);
        }
        finally { _bridgingAsyncPreInitialization = false; }
    }

    /// <summary>
    /// 初始化当前模块。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    public virtual void OnApplicationInitialization(BingModuleInitializationContext context)
    {
        if (context.InitializeModule != null)
            context.InitializeModule(this, context.ServiceProvider);
        else
            UseModule(context.ServiceProvider);
    }

    /// <summary>
    /// 初始化当前模块。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public virtual async Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
        CancellationToken cancellationToken)
    {
        if (_bridgingAsyncInitialization) return;
        if (this is IBingAsyncModuleInitializer initializer)
        {
            _bridgingAsyncInitialization = true;
            try { await initializer.InitializeAsync(context, cancellationToken).ConfigureAwait(false); }
            finally { _bridgingAsyncInitialization = false; }
        }
        else OnApplicationInitialization(context);
    }

    /// <summary>
    /// 在应用主初始化后初始化当前模块。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    public virtual void OnPostApplicationInitialization(BingModuleInitializationContext context)
    {
        if (_bridgingPostInitialization || this is not IOnPostApplicationInitialization initializer ||
            !BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IOnPostApplicationInitialization),
                nameof(IOnPostApplicationInitialization.OnPostApplicationInitialization),
                typeof(BingModuleInitializationContext))) return;
        _bridgingPostInitialization = true;
        try { initializer.OnPostApplicationInitialization(context); }
        finally { _bridgingPostInitialization = false; }
    }

    /// <summary>
    /// 在应用主初始化后初始化当前模块。
    /// </summary>
    /// <param name="context">本轮应用初始化上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public virtual async Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
        CancellationToken cancellationToken)
    {
        if (_bridgingAsyncPostInitialization) return;
        _bridgingAsyncPostInitialization = true;
        try
        {
            if (BingModuleHooks.HasIndependentInterfaceImplementation(this,
                    typeof(IOnPostApplicationInitialization),
                    nameof(IOnPostApplicationInitialization.OnPostApplicationInitializationAsync),
                    typeof(BingModuleInitializationContext), typeof(CancellationToken)))
                await ((IOnPostApplicationInitialization)this)
                    .OnPostApplicationInitializationAsync(context, cancellationToken).ConfigureAwait(false);
            else OnPostApplicationInitialization(context);
        }
        finally { _bridgingAsyncPostInitialization = false; }
    }

    /// <summary>
    /// 关闭当前模块持有的应用资源。
    /// </summary>
    /// <param name="context">本轮应用关闭上下文。</param>
    public virtual void OnApplicationShutdown(BingModuleShutdownContext context)
    {
        if (_bridgingShutdown ||
            !BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IBingModuleShutdown),
                nameof(IBingModuleShutdown.Shutdown), typeof(BingModuleShutdownContext))) return;
        _bridgingShutdown = true;
        try { ((IBingModuleShutdown)this).Shutdown(context); }
        finally { _bridgingShutdown = false; }
    }

    /// <summary>
    /// 关闭当前模块持有的应用资源。
    /// </summary>
    /// <param name="context">本轮应用关闭上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public virtual async Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
        CancellationToken cancellationToken)
    {
        if (_bridgingAsyncShutdown) return;
        if (BingModuleHooks.HasIndependentInterfaceImplementation(this, typeof(IBingModuleShutdown),
                nameof(IBingModuleShutdown.ShutdownAsync), typeof(BingModuleShutdownContext),
                typeof(CancellationToken)))
        {
            _bridgingAsyncShutdown = true;
            try { await ((IBingModuleShutdown)this).ShutdownAsync(context, cancellationToken).ConfigureAwait(false); }
            finally { _bridgingAsyncShutdown = false; }
        }
        else OnApplicationShutdown(context);
    }

    /// <inheritdoc />
    void IBingModuleShutdown.Shutdown(BingModuleShutdownContext context) => OnApplicationShutdown(context);

    /// <inheritdoc />
    Task IBingModuleShutdown.ShutdownAsync(BingModuleShutdownContext context,
        CancellationToken cancellationToken) => OnApplicationShutdownAsync(context, cancellationToken);

    /// <summary>
    /// 获取指定模块直接和间接依赖的模块类型。
    /// </summary>
    /// <param name="moduleType">要检查的模块类型；为空时使用当前实例的类型。</param>
    /// <returns>去重后的依赖模块类型数组。</returns>
    internal Type[] GetDependModuleTypes(Type moduleType = null)
    {
        moduleType ??= GetType();
        return ModuleDependencyGraph.Build(new[] { moduleType }).Types.Skip(1).ToArray();
    }

    #region 辅助方法

    /// <summary>
    /// 判断给定类型是否为可由 Bing 运行时加载的模块类型。
    /// </summary>
    /// <param name="type">待验证的类型。</param>
    /// <returns>有效的 <see cref="BingModule"/> 派生类型且具有公共无参构造函数时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsBingModule(Type type)
    {
        if (type == null)
            return false;
        var typeInfo = type.GetTypeInfo();
        return typeInfo.IsClass &&
               !typeInfo.IsAbstract &&
               !typeInfo.IsGenericType &&
               typeof(BingModule).GetTypeInfo().IsAssignableFrom(type) &&
               type.GetConstructor(Type.EmptyTypes) != null;
    }

    /// <summary>
    /// 验证模块类型是否为可由 Bing 运行时加载的模块类型。
    /// </summary>
    /// <param name="moduleType">待验证的模块类型。</param>
    /// <exception cref="ArgumentException">类型不是有效模块类型时抛出。</exception>
    internal static void CheckBingModuleType(Type moduleType)
    {
        if (moduleType == null)
            throw new ArgumentNullException(nameof(moduleType));
        if (!IsBingModule(moduleType))
            throw new ArgumentException("Given type is not an Bing Module: " + moduleType.AssemblyQualifiedName);
    }

    #endregion

}
