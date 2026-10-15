using AspectCore.Configuration;
using AspectCore.DependencyInjection;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using AspectCore.Extensions.AspectScope;
using AspectCore.Extensions.DependencyInjection;
using Bing.Aspects;
using Bing.Exceptions.Prompts;
using Bing.Extensions;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Bing.DependencyInjection;

/// <summary>
/// AspectCore 扩展
/// </summary>
public static class AspectCoreExtensions
{
    /// <summary>
    /// AspectCore 异常提示实例，保证跨服务集合注册时只保留一个提示组件。
    /// </summary>
    private static readonly AspectExceptionPrompt _aspectExceptionPrompt = new AspectExceptionPrompt();

    /// <summary>
    /// 启用 AspectCore 动态代理及属性注入。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configAction">Aop配置</param>
    /// <remarks>
    /// 默认不开启参数拦截 EnableParameterAspect()，该方法将会导致异常不能更好的定位
    /// 同时自动注册 <see cref="EnableAutowired" /> 所需的属性注入适配器。
    /// </remarks>
    public static void EnableAop(this IServiceCollection services, Action<IAspectConfiguration> configAction = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var isRegistered = services.Any(descriptor => descriptor.ServiceType == typeof(BingAopRegistrationMarker));
        BingParameterValidationRegistration parameterValidationRegistration;
        if (isRegistered == false)
        {
            services.AddSingleton<BingAopRegistrationMarker>();
            services.EnableAutowired();
            services.EnableBingParameterInterceptorSelector();
            parameterValidationRegistration = services
                .Where(descriptor => descriptor.ServiceType == typeof(BingParameterValidationRegistration))
                .Select(descriptor => descriptor.ImplementationInstance)
                .OfType<BingParameterValidationRegistration>()
                .FirstOrDefault() ?? new BingParameterValidationRegistration();
            services.TryAddSingleton(parameterValidationRegistration);
            ExceptionPrompt.AddPrompt(_aspectExceptionPrompt);
            services.ConfigureDynamicProxy(config =>
            {
                //config.EnableParameterAspect();// 启用参数拦截，会导致异常不能很好的定位
                config.NonAspectPredicates.Add(t => Reflections.GetTopBaseType(t.DeclaringType).SafeString() == "Microsoft.EntityFrameworkCore.DbContext");
                config.NonAspectPredicates.Add(IsIgnoredByBingAttribute);
                config.ValidationHandlers.Add(new BingIgnoreAspectValidationHandler());
                config.IgnoreAspectInterfaces();
            });
            services.TryAddScoped<IInterceptorCollector, BingInterceptorCollector>();
            services.RegisterAspectScoped();
        }
        else
        {
            parameterValidationRegistration = services
                .Where(descriptor => descriptor.ServiceType == typeof(BingParameterValidationRegistration))
                .Select(descriptor => descriptor.ImplementationInstance)
                .OfType<BingParameterValidationRegistration>()
                .FirstOrDefault();
        }

        if (configAction != null)
        {
            parameterValidationRegistration ??= new BingParameterValidationRegistration();
            if (isRegistered == false)
                services.TryAddSingleton(parameterValidationRegistration);
            services.ConfigureDynamicProxy(config =>
            {
                var interceptorCount = config.Interceptors.Count;
                var validationHandlerCount = config.ValidationHandlers.Count;
                configAction(config);
                if (HasParameterAspect(config, interceptorCount, validationHandlerCount))
                    parameterValidationRegistration.Enabled = true;
            });
        }
    }

    /// <summary>
    /// 判断方法是否带有 Bing 忽略拦截标记。
    /// </summary>
    /// <param name="method">待检查的方法。</param>
    /// <returns>需要跳过拦截时返回 <see langword="true" />。</returns>
    private static bool IsIgnoredByBingAttribute(MethodInfo method)
    {
        if (method == null)
            return false;

        if (HasIgnoreMarker(method) || HasIgnoreMarker(method.DeclaringType))
            return true;

        foreach (var candidate in GetRelatedMethods(method))
        {
            if (HasIgnoreMarker(candidate) || HasIgnoreMarker(candidate.DeclaringType))
                return true;
            if (HasIgnoreMarker(FindRelatedProperty(candidate)))
                return true;
        }

        return HasIgnoreMarker(FindRelatedProperty(method));
    }

    /// <summary>
    /// 判断成员是否带有 Bing 忽略标记。
    /// </summary>
    /// <param name="member">待检查成员。</param>
    /// <returns>带有标记时返回 <see langword="true" />。</returns>
    private static bool HasIgnoreMarker(MemberInfo member)
    {
        return member?.IsDefined(typeof(IgnoreAttribute), inherit: true) == true;
    }

    /// <summary>
    /// 获取方法对应的基类和接口声明。
    /// </summary>
    /// <param name="method">待检查方法。</param>
    /// <returns>相关方法集合。</returns>
    private static IEnumerable<MethodInfo> GetRelatedMethods(MethodInfo method)
    {
        var methods = new HashSet<MethodInfo>();
        AddMethod(method);
        AddMethod(method.GetBaseDefinition());

        var declaringType = method.DeclaringType;
        if (declaringType != null)
        {
            foreach (var @interface in declaringType.GetInterfaces())
            {
                InterfaceMapping map;
                try
                {
                    map = declaringType.GetInterfaceMap(@interface);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                for (var index = 0; index < map.TargetMethods.Length; index++)
                {
                    if (map.TargetMethods[index] == method ||
                        map.TargetMethods[index].GetBaseDefinition() == method.GetBaseDefinition())
                        AddMethod(map.InterfaceMethods[index]);
                }
            }
        }

        return methods;

        void AddMethod(MethodInfo item)
        {
            if (item != null)
                methods.Add(item);
        }
    }

    /// <summary>
    /// 查找方法对应的属性。
    /// </summary>
    /// <param name="method">待检查的方法。</param>
    /// <returns>对应属性；不存在时返回 <see langword="null" />。</returns>
    private static PropertyInfo FindRelatedProperty(MethodInfo method)
    {
        return method?.DeclaringType?.GetProperties(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(item => item.GetMethod == method || item.SetMethod == method);
    }

    /// <summary>
    /// 启用 Bing 中立参数标记的 AspectCore 执行器。
    /// </summary>
    /// <param name="configuration">AspectCore 配置对象。</param>
    /// <returns>当前配置对象。</returns>
    public static IAspectConfiguration EnableBingParameterValidation(this IAspectConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        configuration.ValidationHandlers.Add(new BingParameterValidationEnabledHandler());
        return configuration.EnableParameterAspect();
    }

    /// <summary>
    /// 判断 AspectCore 配置是否包含参数拦截器。
    /// </summary>
    /// <param name="configuration">AspectCore 配置对象。</param>
    /// <param name="initialInterceptorCount">配置回调执行前的拦截器数量。</param>
    /// <param name="initialValidationHandlerCount">配置回调执行前的验证处理器数量。</param>
    /// <returns>包含参数拦截器时返回 <see langword="true" />。</returns>
    private static bool HasParameterAspect(
        IAspectConfiguration configuration,
        int initialInterceptorCount,
        int initialValidationHandlerCount)
    {
        if (configuration.ValidationHandlers.Skip(initialValidationHandlerCount)
            .OfType<BingParameterValidationEnabledHandler>().Any())
            return true;

        foreach (var factory in configuration.Interceptors.Skip(initialInterceptorCount))
        {
            var interceptorType = factory.GetType().GetField(
                "_interceptorType",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(factory) as Type;
            if (interceptorType == typeof(EnableParameterAspectInterceptor))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 注册 Bing 参数选择器，并保留调用方已注册的选择器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    private static void EnableBingParameterInterceptorSelector(this IServiceCollection services)
    {
        var existing = services
            .Where(x => x.ServiceType == typeof(IParameterInterceptorSelector))
            .ToArray();
        foreach (var descriptor in existing)
        {
            if (descriptor.ImplementationType == typeof(BingParameterInterceptorSelector))
                continue;

            services.Remove(descriptor);
            services.Add(ServiceDescriptor.Scoped<IBingParameterInterceptorSelector>(provider =>
            {
                var selector = descriptor.ImplementationInstance as IParameterInterceptorSelector;
                if (selector == null && descriptor.ImplementationFactory != null)
                    selector = descriptor.ImplementationFactory(provider) as IParameterInterceptorSelector;
                if (selector == null && descriptor.ImplementationType != null)
                    selector = ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType) as IParameterInterceptorSelector;
                return new BingParameterInterceptorSelectorAdapter(selector);
            }));
        }
        services.TryAddScoped<IParameterInterceptorSelector, BingParameterInterceptorSelector>();
    }

    /// <summary>
    /// 启用 AspectCore 属性注入。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>当前服务集合。</returns>
    /// <remarks>
    /// 调用方仍需通过 <c>UseServiceContext</c> 启用 AspectCore 服务上下文。
    /// </remarks>
    public static IServiceCollection EnableAutowired(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddScoped<IPropertyInjectorFactory, BingPropertyInjectorFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IServiceResolveCallback, BingPropertyInjectorCallback>());
        return services;
    }

    /// <summary>
    /// 注册拦截作用域
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void RegisterAspectScoped(this IServiceCollection services)
    {
        services.TryAddScoped<IAspectScheduler, ScopeAspectScheduler>();
        services.TryAddScoped<IAspectBuilderFactory, ScopeAspectBuilderFactory>();
        services.TryAddScoped<IAspectContextFactory, ScopeAspectContextFactory>();
    }

    /// <summary>
    /// 忽略拦截接口
    /// </summary>
    /// <param name="configuration">拦截器配置对象。</param>
    public static void IgnoreAspectInterfaces(this IAspectConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        configuration.NonAspectPredicates.Add(IsIgnoredBingInterface);
    }

    /// <summary>
    /// 判断方法所属接口是否声明忽略拦截标记。
    /// </summary>
    /// <param name="method">待检查的方法。</param>
    /// <returns>接口声明忽略标记时返回 <see langword="true" />。</returns>
    private static bool IsIgnoredBingInterface(MethodInfo method)
    {
        return method?.DeclaringType?.IsInterface == true &&
            HasIgnoreMarker(method.DeclaringType);
    }

    /// <summary>
    /// 是否创建代理
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="isEnableAopProxy">是否启用IAopProxy接口标记</param>
    /// <returns>类型应创建 AOP 代理时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    private static bool IsProxy(Type type, bool isEnableAopProxy)
    {
        if (type == null)
            return false;
        if (isEnableAopProxy == false)
        {
            if (Reflections.GetTopBaseType(type).SafeString() == "Microsoft.EntityFrameworkCore.DbContext")
                return false;
            if (type.SafeString().Contains("Xunit.DependencyInjection.ITestOutputHelperAccessor"))
                return false;
            return true;
        }
        var interfaces = type.GetInterfaces();
        if (interfaces == null || interfaces.Length == 0)
            return false;
        foreach (var item in interfaces)
        {
            if (item == typeof(IAopProxy))
                return true;
        }
        return false;
    }
}

/// <summary>
/// 标记当前服务集合已经注册 AspectCore 适配器。
/// </summary>
internal sealed class BingAopRegistrationMarker
{
}

/// <summary>
/// 在 AspectCore 验证阶段排除带有 Bing 忽略标记的方法。
/// </summary>
internal sealed class BingIgnoreAspectValidationHandler : IAspectValidationHandler
{
    /// <inheritdoc />
    public int Order => int.MinValue;

    /// <inheritdoc />
    public bool Invoke(AspectValidationContext context, AspectValidationDelegate next)
    {
        if (IsIgnored(context.Method))
            return false;
        return next(context);
    }

    /// <summary>
    /// 判断方法或其接口声明是否带有 Bing 忽略标记。
    /// </summary>
    /// <param name="method">待检查方法。</param>
    /// <returns>需要跳过拦截时返回 <see langword="true" />。</returns>
    private static bool IsIgnored(MethodInfo method)
    {
        if (method == null)
            return false;

        if (method.IsDefined(typeof(IgnoreAttribute), inherit: true) ||
            method.DeclaringType?.IsDefined(typeof(IgnoreAttribute), inherit: true) == true)
            return true;

        var declaringType = method.DeclaringType;
        if (declaringType == null)
            return false;

        foreach (var @interface in declaringType.GetInterfaces())
        {
            InterfaceMapping map;
            try
            {
                map = declaringType.GetInterfaceMap(@interface);
            }
            catch (ArgumentException)
            {
                continue;
            }

            for (var index = 0; index < map.TargetMethods.Length; index++)
            {
                if (map.TargetMethods[index] != method &&
                    map.TargetMethods[index].GetBaseDefinition() != method.GetBaseDefinition())
                    continue;
                if (map.InterfaceMethods[index].IsDefined(typeof(IgnoreAttribute), inherit: true) ||
                    @interface.IsDefined(typeof(IgnoreAttribute), inherit: true))
                    return true;
            }
        }

        return false;
    }
}
