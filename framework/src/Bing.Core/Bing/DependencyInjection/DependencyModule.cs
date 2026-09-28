using System.ComponentModel;
using Bing.Core.Modularity;
using Bing.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.DependencyInjection;

/// <summary>
/// 依赖注入模块
/// </summary>
[Description("依赖注入模块")]
public class DependencyModule : BingModule
{
    /// <inheritdoc />
    /// <remarks>依赖注入模块属于核心级别。</remarks>
    public override ModuleLevel Level => ModuleLevel.Core;

    /// <inheritdoc />
    /// <remarks>在核心级别中使用顺序值 1。</remarks>
    public override int Order => 1;

    /// <inheritdoc />
    /// <remarks>注册依赖注入基础设施，并按配置扫描和注册依赖服务。</remarks>
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        // 服务定位器设置
        var registration = BingModuleRegistration.Get(services);

        services.AddTransient(typeof(Lazy<>), typeof(Lazier<>));// 解决循环依赖问题
        services.TryAddTransient<IHybridServiceScopeFactory, DefaultServiceScopeFactory>();
        services.AddScoped<ScopedDictionary>();

        if (registration?.ConventionServicesRegistered != true)
            RegisterConventionServices(services);

        return services;
    }

    /// <summary>
    /// 注册选中模块程序集中的约定服务。
    /// </summary>
    internal void RegisterConventionServices(IServiceCollection services)
    {
        var registration = BingModuleRegistration.Get(services);
        if (registration?.AutoRegisterServices == false) return;
        var dependencyTypeFinder = services.GetOrAddTypeFinder<IDependencyTypeFinder>(assemblyFinder => new DependencyTypeFinder(assemblyFinder));
        foreach (var dependencyType in dependencyTypeFinder.FindAll(true))
            AddToServices(services, dependencyType);
    }

    /// <inheritdoc />
    /// <remarks>标记模块已启用；静态兼容入口的定位器绑定由运行时负责。</remarks>
    public override void UseModule(IServiceProvider provider)
    {
        // 静态兼容入口由运行时绑定和解绑，新入口不依赖全局定位器。
        Enabled = true;
    }

    /// <summary>
    /// 将服务实现类型注册到服务集合中
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="implementationType">要注册的服务实现类型</param>
    protected virtual void AddToServices(IServiceCollection services, Type implementationType)
    {
        if (implementationType.IsAbstract || implementationType.IsInterface)
            return;

        var lifetime = GetLifetimeOrNull(implementationType);
        if (lifetime == null)
            return;

        var dependencyAttribute = implementationType.GetAttribute<DependencyAttribute>();
        var serviceTypes = GetImplementedInterfaces(implementationType);

        // 服务数量为0时注册自身
        if (serviceTypes.Length == 0)
        {
            services.TryAdd(new ServiceDescriptor(implementationType, implementationType, lifetime.Value));
            return;
        }

        // 服务实现显式要求注册自身时，注册自身并且继续注册接口
        if (dependencyAttribute?.AddSelf == true)
            services.TryAdd(new ServiceDescriptor(implementationType, implementationType, lifetime.Value));

        // 注册服务
        for (var i = 0; i < serviceTypes.Length; i++)
        {
            var serviceType = serviceTypes[i];
            var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime.Value);
            if (lifetime.Value == ServiceLifetime.Transient)
            {
                services.TryAddEnumerable(descriptor);
                continue;
            }

            var multiple = serviceType.HasAttribute<MultipleDependencyAttribute>();
            if (i == 0)
            {
                if (multiple)
                    services.Add(descriptor);
                else
                    AddSingleService(services, descriptor, dependencyAttribute);
            }
            else
            {
                if (multiple)
                {
                    services.Add(descriptor);
                }
                else
                {
                    // 有多个接口，后边的接口注册使用第一个接口的实例，保证同个实现类的多个接口获得同一实例
                    var firstServiceType = serviceTypes[0];
                    descriptor = new ServiceDescriptor(serviceType, provider => provider.GetService(firstServiceType), lifetime.Value);
                    AddSingleService(services, descriptor, dependencyAttribute);
                }
            }
        }
    }

    /// <summary>
    /// 重写以实现 从类型获取要注册的 <see cref="ServiceLifetime"/> 生命周期类型
    /// </summary>
    /// <param name="type">依赖注入实现类型</param>
    /// <returns>解析到的服务生命周期；无法解析时返回 <see langword="null"/>。</returns>
    protected virtual ServiceLifetime? GetLifetimeOrNull(Type type)
    {
        var attribute = type.GetAttribute<DependencyAttribute>();
        if (attribute != null)
            return attribute.Lifetime;
        if (type.IsDeriveClassFrom<ITransientDependency>())
            return ServiceLifetime.Transient;
        if (type.IsDeriveClassFrom<IScopedDependency>())
            return ServiceLifetime.Scoped;
        if (type.IsDeriveClassFrom<ISingletonDependency>())
            return ServiceLifetime.Singleton;
        return null;
    }

    /// <summary>
    /// 重写以实现 获取实现类型的所有可注册服务接口
    /// </summary>
    /// <param name="type">依赖注入实现类型</param>
    /// <returns>实现类型可注册的服务接口数组。</returns>
    protected virtual Type[] GetImplementedInterfaces(Type type)
    {
        var exceptInterfaces = new[] { typeof(IDisposable) };
        var interfaceTypes = type.GetInterfaces()
            .Where(x => !exceptInterfaces.Contains(x) && !x.HasAttribute<IgnoreDependencyAttribute>())
            .ToArray();
        for (var index = 0; index < interfaceTypes.Length; index++)
        {
            var interfaceType = interfaceTypes[index];
            if (interfaceType.IsGenericType && !interfaceType.IsGenericTypeDefinition && interfaceType.FullName == null)
                interfaceTypes[index] = interfaceType.GetGenericTypeDefinition();
        }
        return interfaceTypes;
    }

    /// <summary>
    /// 根据依赖注入特性将单个服务描述添加到服务集合。
    /// </summary>
    /// <param name="services">要写入服务描述的服务集合。</param>
    /// <param name="descriptor">待注册的服务描述。</param>
    /// <param name="dependencyAttribute">控制替换、尝试添加或普通添加行为的依赖注入特性。</param>
    private static void AddSingleService(IServiceCollection services, ServiceDescriptor descriptor, DependencyAttribute dependencyAttribute)
    {
        if (dependencyAttribute?.ReplaceExisting == true)
            services.Replace(descriptor);
        else if (dependencyAttribute?.TryAdd == true)
            services.TryAdd(descriptor);
        else
            services.Add(descriptor);
    }
}
