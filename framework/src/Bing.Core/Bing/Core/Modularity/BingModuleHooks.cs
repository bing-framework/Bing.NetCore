using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 按模块实际重写选择每个生命周期阶段的唯一入口。
/// </summary>
internal static class BingModuleHooks
{
    /// <summary>
    /// 判断模块是否重写了基类虚钩子。
    /// </summary>
    /// <param name="module">待检查模块。</param>
    /// <param name="name">钩子名称。</param>
    /// <param name="parameters">钩子参数类型。</param>
    /// <returns>存在有效重写时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    private static bool Overrides(BingModule module, string name, params Type[] parameters)
    {
        var baseMethod = typeof(BingModule).GetMethod(name, BindingFlags.Public | BindingFlags.Instance,
            null, parameters, null);
        for (var type = module.GetType(); type != null && type != typeof(BingModule); type = type.BaseType)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null, parameters, null);
            if (method != null && method.GetBaseDefinition() == baseMethod)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 判断旧接口实现是否独立于基类虚钩子。
    /// </summary>
    /// <param name="module">待检查模块。</param>
    /// <param name="interfaceType">旧接口类型。</param>
    /// <param name="methodName">接口方法名称。</param>
    /// <param name="parameters">接口方法参数类型。</param>
    /// <returns>存在独立接口实现时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal static bool HasIndependentInterfaceImplementation(BingModule module, Type interfaceType,
        string methodName, params Type[] parameters)
    {
        if (!interfaceType.IsInstanceOfType(module)) return false;
        var mapping = module.GetType().GetInterfaceMap(interfaceType);
        var index = Array.FindIndex(mapping.InterfaceMethods, method =>
            method.Name == methodName && method.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(parameters));
        if (index < 0) return false;
        var baseMethod = typeof(BingModule).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance,
            null, parameters, null);
        return baseMethod == null || mapping.TargetMethods[index].GetBaseDefinition() != baseMethod;
    }

    /// <summary>
    /// 判断同步注册是否会遗漏异步配置钩子。
    /// </summary>
    /// <param name="module">待检查模块。</param>
    /// <returns>需要异步配置时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal static bool RequiresAsyncConfiguration(BingModule module) =>
        Overrides(module, nameof(BingModule.PreConfigureServicesAsync), typeof(IServiceCollection), typeof(CancellationToken)) ||
        Overrides(module, nameof(BingModule.ConfigureServicesAsync), typeof(IServiceCollection), typeof(CancellationToken)) ||
        Overrides(module, nameof(BingModule.PostConfigureServicesAsync), typeof(IServiceCollection), typeof(CancellationToken));

    /// <summary>
    /// 判断旧入口是否无法执行模块的新服务配置钩子。
    /// </summary>
    /// <param name="module">待检查模块。</param>
    /// <returns>存在新服务配置钩子时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal static bool HasNewServiceHooks(BingModule module) => RequiresAsyncConfiguration(module) ||
        Overrides(module, nameof(BingModule.PreConfigureServices), typeof(IServiceCollection)) ||
        Overrides(module, nameof(BingModule.ConfigureServices), typeof(IServiceCollection)) ||
        Overrides(module, nameof(BingModule.PostConfigureServices), typeof(IServiceCollection));

    /// <summary>
    /// 判断同步初始化是否会遗漏异步钩子。
    /// </summary>
    /// <param name="module">待检查模块。</param>
    /// <returns>需要异步初始化时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal static bool RequiresAsyncInitialization(BingModule module) =>
        Overrides(module, nameof(BingModule.OnPreApplicationInitializationAsync),
            typeof(BingModuleInitializationContext), typeof(CancellationToken)) ||
        Overrides(module, nameof(BingModule.OnApplicationInitializationAsync),
            typeof(BingModuleInitializationContext), typeof(CancellationToken)) ||
        Overrides(module, nameof(BingModule.OnPostApplicationInitializationAsync),
            typeof(BingModuleInitializationContext), typeof(CancellationToken)) ||
        (module is IBingAsyncModuleInitializer &&
         !Overrides(module, nameof(BingModule.OnApplicationInitialization), typeof(BingModuleInitializationContext)));

    /// <summary>
    /// 判断同步关闭是否会遗漏异步清理。
    /// </summary>
    /// <param name="module">待检查模块。</param>
    /// <returns>需要异步关闭时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal static bool RequiresAsyncShutdown(BingModule module) =>
        Overrides(module, nameof(BingModule.OnApplicationShutdownAsync),
            typeof(BingModuleShutdownContext), typeof(CancellationToken)) ||
        (module is IBingAsyncModuleShutdown && module is not IBingModuleShutdown &&
         !Overrides(module, nameof(BingModule.OnApplicationShutdown), typeof(BingModuleShutdownContext)));

    /// <summary>
    /// 判断模块在初始化阶段是否执行实际钩子。
    /// </summary>
    /// <param name="module">待检查模块。</param>
    /// <param name="phase">初始化阶段。</param>
    /// <returns>该阶段存在实际回调时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal static bool HasInitializationHook(BingModule module, string phase) => phase switch
    {
        "PreInitialize" => Overrides(module, nameof(BingModule.OnPreApplicationInitialization),
                               typeof(BingModuleInitializationContext)) ||
                           Overrides(module, nameof(BingModule.OnPreApplicationInitializationAsync),
                               typeof(BingModuleInitializationContext), typeof(CancellationToken)),
        "Initialize" => true,
        "PostInitialize" => Overrides(module, nameof(BingModule.OnPostApplicationInitialization),
                                typeof(BingModuleInitializationContext)) ||
                            Overrides(module, nameof(BingModule.OnPostApplicationInitializationAsync),
                                typeof(BingModuleInitializationContext), typeof(CancellationToken)),
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    /// <summary>
    /// 执行服务配置阶段。
    /// </summary>
    internal static Task ConfigureAsync(BingModule module, IServiceCollection services, string phase,
        bool asynchronous, CancellationToken token)
    {
        switch (phase)
        {
            case "PreConfigureServices":
                if (asynchronous && Overrides(module, nameof(BingModule.PreConfigureServicesAsync),
                        typeof(IServiceCollection), typeof(CancellationToken)))
                    return module.PreConfigureServicesAsync(services, token);
                if (Overrides(module, nameof(BingModule.PreConfigureServices), typeof(IServiceCollection)))
                    module.PreConfigureServices(services);
                else if (module is IBingPreConfigureServices)
                    module.PreConfigureServices(services);
                return Task.CompletedTask;
            case "ConfigureServices":
                if (asynchronous && Overrides(module, nameof(BingModule.ConfigureServicesAsync),
                        typeof(IServiceCollection), typeof(CancellationToken)))
                    return module.ConfigureServicesAsync(services, token);
                if (Overrides(module, nameof(BingModule.ConfigureServices), typeof(IServiceCollection)))
                    module.ConfigureServices(services);
                else
                    module.AddServices(services);
                return Task.CompletedTask;
            case "PostConfigureServices":
                if (asynchronous && Overrides(module, nameof(BingModule.PostConfigureServicesAsync),
                        typeof(IServiceCollection), typeof(CancellationToken)))
                    return module.PostConfigureServicesAsync(services, token);
                if (Overrides(module, nameof(BingModule.PostConfigureServices), typeof(IServiceCollection)))
                    module.PostConfigureServices(services);
                else if (module is IBingPostConfigureServices)
                    module.PostConfigureServices(services);
                return Task.CompletedTask;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase));
        }
    }

    /// <summary>
    /// 执行应用初始化阶段。
    /// </summary>
    internal static Task InitializeAsync(BingModule module, BingModuleInitializationContext context,
        string phase, bool asynchronous, CancellationToken token)
    {
        switch (phase)
        {
            case "PreInitialize":
                if (asynchronous && Overrides(module, nameof(BingModule.OnPreApplicationInitializationAsync),
                        typeof(BingModuleInitializationContext), typeof(CancellationToken)))
                    return module.OnPreApplicationInitializationAsync(context, token);
                module.OnPreApplicationInitialization(context);
                return Task.CompletedTask;
            case "Initialize":
                if (asynchronous && Overrides(module, nameof(BingModule.OnApplicationInitializationAsync),
                        typeof(BingModuleInitializationContext), typeof(CancellationToken)))
                    return module.OnApplicationInitializationAsync(context, token);
                if (Overrides(module, nameof(BingModule.OnApplicationInitialization), typeof(BingModuleInitializationContext)))
                    module.OnApplicationInitialization(context);
                else if (asynchronous && module is IBingAsyncModuleInitializer)
                    return module.OnApplicationInitializationAsync(context, token);
                else
                    module.OnApplicationInitialization(context);
                return Task.CompletedTask;
            case "PostInitialize":
                if (asynchronous && Overrides(module, nameof(BingModule.OnPostApplicationInitializationAsync),
                        typeof(BingModuleInitializationContext), typeof(CancellationToken)))
                    return module.OnPostApplicationInitializationAsync(context, token);
                module.OnPostApplicationInitialization(context);
                return Task.CompletedTask;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase));
        }
    }

    /// <summary>
    /// 执行应用关闭阶段。
    /// </summary>
    internal static Task ShutdownAsync(BingModule module, BingModuleShutdownContext context,
        bool asynchronous, CancellationToken token)
    {
        if (asynchronous && Overrides(module, nameof(BingModule.OnApplicationShutdownAsync),
                typeof(BingModuleShutdownContext), typeof(CancellationToken)))
            return module.OnApplicationShutdownAsync(context, token);
        if (Overrides(module, nameof(BingModule.OnApplicationShutdown), typeof(BingModuleShutdownContext)))
            module.OnApplicationShutdown(context);
        else if (asynchronous && module is IBingAsyncModuleShutdown)
            return module.OnApplicationShutdownAsync(context, token);
        else if (module is IBingModuleShutdown)
            module.OnApplicationShutdown(context);
        return Task.CompletedTask;
    }
}
