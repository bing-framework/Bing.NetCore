using AspectCore.DynamicProxy;
using AspectCore.DependencyInjection;
using AspectCore.Extensions.AspectScope;
using AspectCore.Extensions.DependencyInjection;
using Bing.Application.Aspects;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Bing.DependencyInjection;

/// <summary>
/// 注册 Bing 工作单元的 AspectCore 适配器。
/// </summary>
public static class BingUnitOfWorkExtensions
{
    /// <summary>
    /// 启用中立工作单元标记的 AspectCore 执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>当前服务集合。</returns>
    public static IServiceCollection EnableBingUnitOfWork(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (services.Any(descriptor => descriptor.ServiceType == typeof(BingUnitOfWorkRegistrationMarker)))
            return services;

        services.AddSingleton<BingUnitOfWorkRegistrationMarker>();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAdditionalInterceptorSelector, BingUnitOfWorkInterceptorSelector>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IServiceResolveCallback, BingUnitOfWorkProxyCallback>());
        services.ConfigureDynamicProxy(configuration =>
            configuration.ValidationHandlers.Add(new BingUnitOfWorkValidationHandler()));
        return services;
    }
}

/// <summary>
/// 标记工作单元适配器已经注册，避免重复追加动态代理配置。
/// </summary>
internal sealed class BingUnitOfWorkRegistrationMarker
{
}

/// <summary>
/// 为仅在实现方法上声明中立工作单元标记且尚未生成代理的接口服务补充代理。
/// </summary>
internal sealed class BingUnitOfWorkProxyCallback : IServiceResolveCallback
{
    /// <inheritdoc />
    public object Invoke(IServiceResolver resolver, object instance, ServiceDefinition definition)
    {
        if (resolver == null || instance == null || definition?.ServiceType?.IsInterface != true)
            return instance;

        if (IsAspectCoreProxy(instance.GetType()) ||
            HasUnitOfWorkTypeMarker(definition.ServiceType, instance.GetType()) ||
            HasUnitOfWorkServiceMethodMarker(definition.ServiceType) ||
            HasUnitOfWorkImplementationMethodMarker(instance.GetType()) == false)
            return instance;

        var generator = resolver.Resolve(typeof(IProxyGenerator)) as IProxyGenerator;
        return generator?.CreateInterfaceProxy(definition.ServiceType, instance) ?? instance;
    }

    /// <summary>
    /// 判断服务接口或实现类型是否声明工作单元标记。
    /// </summary>
    /// <param name="serviceType">服务接口类型。</param>
    /// <param name="implementationType">实现类型。</param>
    /// <returns>存在工作单元标记时返回 <see langword="true" />。</returns>
    private static bool HasUnitOfWorkTypeMarker(Type serviceType, Type implementationType)
    {
        if (serviceType.IsDefined(typeof(UnitOfWorkAttribute), inherit: true) ||
            implementationType.IsDefined(typeof(UnitOfWorkAttribute), inherit: true))
            return true;

        return false;
    }

    /// <summary>
    /// 判断服务契约是否声明工作单元方法标记。
    /// </summary>
    /// <param name="serviceType">服务接口类型。</param>
    /// <returns>存在方法标记时返回 <see langword="true" />。</returns>
    private static bool HasUnitOfWorkServiceMethodMarker(Type serviceType)
    {
        return serviceType.GetMethods().Any(method =>
            method.IsDefined(typeof(UnitOfWorkAttribute), inherit: true));
    }

    /// <summary>
    /// 判断实现类型是否声明工作单元方法标记。
    /// </summary>
    /// <param name="implementationType">实现类型。</param>
    /// <returns>存在方法标记时返回 <see langword="true" />。</returns>
    private static bool HasUnitOfWorkImplementationMethodMarker(Type implementationType)
    {
        return implementationType.GetMethods().Any(method =>
            method.IsDefined(typeof(UnitOfWorkAttribute), inherit: true));
    }

    /// <summary>
    /// 判断实例是否已由 AspectCore 生成代理。
    /// </summary>
    /// <param name="type">实例类型。</param>
    /// <returns>为 AspectCore 代理时返回 <see langword="true" />。</returns>
    private static bool IsAspectCoreProxy(Type type)
    {
        return type.Assembly.GetName().Name == "AspectCore.DynamicProxy.Generator" ||
            type.FullName?.StartsWith("AspectCore.DynamicGenerated.", StringComparison.Ordinal) == true;
    }
}

/// <summary>
/// 让包含中立工作单元标记的方法进入代理验证范围。
/// </summary>
internal sealed class BingUnitOfWorkValidationHandler : IAspectValidationHandler
{
    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool Invoke(AspectValidationContext context, AspectValidationDelegate next)
    {
        if (context.Method?.IsDefined(typeof(UnitOfWorkAttribute), inherit: true) == true)
            return true;
        if (context.Method?.DeclaringType?.IsDefined(typeof(UnitOfWorkAttribute), inherit: true) == true)
            return true;
        return next(context);
    }
}

/// <summary>
/// 选择带有中立工作单元标记的方法拦截器。
/// </summary>
internal sealed class BingUnitOfWorkInterceptorSelector : IAdditionalInterceptorSelector
{
    /// <inheritdoc />
    public IEnumerable<IInterceptor> Select(MethodInfo serviceMethod, MethodInfo implementationMethod)
    {
        var attribute = FindAttribute(serviceMethod, implementationMethod);
        if (attribute == null)
            return Array.Empty<IInterceptor>();

        return new IInterceptor[]
        {
            new BingUnitOfWorkInterceptor(attribute)
        };
    }

    /// <summary>
    /// 查找服务方法、实现方法或声明类型上的工作单元标记。
    /// </summary>
    /// <param name="serviceMethod">服务方法。</param>
    /// <param name="implementationMethod">实现方法。</param>
    /// <returns>找到的标记；未找到时返回 <see langword="null" />。</returns>
    private static UnitOfWorkAttribute FindAttribute(MethodInfo serviceMethod, MethodInfo implementationMethod)
    {
        var attribute = serviceMethod?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
        if (attribute != null)
            return attribute;

        attribute = implementationMethod?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
        if (attribute != null)
            return attribute;

        attribute = serviceMethod?.DeclaringType?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
        if (attribute != null)
            return attribute;

        return implementationMethod?.DeclaringType?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
    }
}

/// <summary>
/// 执行工作单元提交的 AspectCore 拦截器。
/// </summary>
internal sealed class BingUnitOfWorkInterceptor : AbstractInterceptorAttribute, IScopeInterceptor
{
    /// <summary>
    /// 初始化一个 <see cref="BingUnitOfWorkInterceptor" /> 类型的实例。
    /// </summary>
    /// <param name="attribute">中立工作单元标记。</param>
    public BingUnitOfWorkInterceptor(UnitOfWorkAttribute attribute)
    {
        if (attribute == null)
            throw new ArgumentNullException(nameof(attribute));

        Order = attribute.Order;
        Scope = MapScope(attribute.Scope);
        Inherited = true;
    }

    /// <inheritdoc />
    public Scope Scope { get; set; }

    /// <inheritdoc />
    public override async Task Invoke(AspectContext context, AspectDelegate next)
    {
        await next(context).ConfigureAwait(false);
        var manager = context.ServiceProvider.GetService<IUnitOfWorkManager>();
        if (manager == null)
            return;

        await manager.CommitAsync().ConfigureAwait(false);
        if (context.Implementation is ICommitAfter service)
            service.CommitAfter();
    }

    /// <summary>
    /// 将中立工作单元范围映射为 AspectCore 范围。
    /// </summary>
    /// <param name="scope">中立工作单元范围。</param>
    /// <returns>AspectCore 工作范围。</returns>
    private static Scope MapScope(BingUnitOfWorkScope scope)
    {
        return scope switch
        {
            BingUnitOfWorkScope.None => Scope.None,
            BingUnitOfWorkScope.Nested => Scope.Nested,
            _ => Scope.Aspect
        };
    }
}
