using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 注册 Bing 动态 API 服务端能力。
/// </summary>
public static class BingDynamicApiServiceCollectionExtensions
{
    /// <summary>
    /// 将指定程序集中的应用服务按约定注册为 MVC API 控制器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="assembly">待扫描的应用服务程序集。</param>
    /// <param name="configure">动态 API 选项配置。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddBingDynamicApi(
        this IServiceCollection services,
        Assembly assembly,
        Action<BingDynamicApiOptions> configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        var options = new BingDynamicApiOptions();
        configure?.Invoke(options);
        var registryDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(BingDynamicApiRegistry));
        var registry = registryDescriptor?.ImplementationInstance as BingDynamicApiRegistry;
        if (registry == null)
        {
            registry = new BingDynamicApiRegistry();
            services.AddSingleton(registry);
        }
        services.TryAddTransient<BingDynamicApiRouteValueModelBinder>();
        registry.AddAssembly(assembly, options);
        RegisterControllerServiceAliases(services, registry);

        var mvcBuilder = services.AddControllers();
        var partManager = mvcBuilder.PartManager;
        AddAssemblyPart(partManager, assembly);
        AddAssemblyPart(partManager, typeof(BingDynamicApiDescriptionController).Assembly);
        if (!partManager.FeatureProviders.OfType<BingDynamicApiControllerFeatureProvider>().Any())
            partManager.FeatureProviders.Add(new BingDynamicApiControllerFeatureProvider(registry));
        mvcBuilder.AddControllersAsServices();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(BingDynamicApiMvcRegistration)))
        {
            services.AddSingleton<BingDynamicApiMvcRegistration>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IActionDescriptorProvider, BingDynamicApiActionDescriptorProvider>());
            services.AddControllers().AddMvcOptions(options =>
                options.Conventions.Add(new BingDynamicApiApplicationModelConvention(registry)));
        }

        return services;
    }

    /// <summary>
    /// 添加尚未注册的 MVC 程序集部件。
    /// </summary>
    /// <param name="manager">MVC 应用部件管理器。</param>
    /// <param name="assembly">待添加程序集。</param>
    private static void AddAssemblyPart(ApplicationPartManager manager, Assembly assembly)
    {
        var name = assembly.GetName().Name;
        if (manager.ApplicationParts.Any(part => string.Equals(part.Name, name, StringComparison.Ordinal)))
            return;
        manager.ApplicationParts.Add(new AssemblyPart(assembly));
    }

    /// <summary>
    /// 将动态控制器类型映射到服务契约的 DI 实例。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="registry">动态服务注册表。</param>
    /// <remarks>沿用契约注册的生命周期，使控制器调用保留服务代理行为。</remarks>
    private static void RegisterControllerServiceAliases(IServiceCollection services, BingDynamicApiRegistry registry)
    {
        foreach (var registration in registry.Services)
        {
            var serviceDescriptor = services.LastOrDefault(descriptor => descriptor.ServiceType == registration.ServiceInterface);
            var lifetime = serviceDescriptor?.Lifetime ?? ServiceLifetime.Scoped;
            services.RemoveAll(registration.ControllerType);
            services.Add(new ServiceDescriptor(
                registration.ControllerType,
                provider => provider.GetRequiredService(registration.ServiceInterface),
                lifetime));
        }
    }

    /// <summary>
    /// 标记当前宿主已注册动态 MVC 约定。
    /// </summary>
    private sealed class BingDynamicApiMvcRegistration
    {
    }
}
