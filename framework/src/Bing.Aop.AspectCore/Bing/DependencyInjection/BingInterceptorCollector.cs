using System.Reflection;
using AspectCore.DependencyInjection;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using Bing.Aspects;
using System.Linq;

namespace Bing.DependencyInjection;

/// <summary>
/// 在 AspectCore 收集拦截器时应用 Bing 的中立忽略标记。
/// </summary>
internal sealed class BingInterceptorCollector : IInterceptorCollector
{
    /// <summary>
    /// AspectCore 默认的拦截器收集器。
    /// </summary>
    private readonly InterceptorCollector _inner;

    /// <summary>
    /// 当前服务集合的参数验证注册状态。
    /// </summary>
    private readonly BingParameterValidationRegistration _parameterValidationRegistration;
    /// <summary>
    /// 初始化一个 <see cref="BingInterceptorCollector" /> 类型的实例。
    /// </summary>
    /// <param name="selectors">普通拦截器选择器。</param>
    /// <param name="additionalSelectors">附加拦截器选择器。</param>
    /// <param name="propertyInjectorFactory">属性注入器工厂。</param>
    /// <param name="cachingProvider">拦截器缓存提供程序。</param>
    /// <param name="parameterValidationRegistration">参数验证注册状态。</param>
    public BingInterceptorCollector(
        IEnumerable<IInterceptorSelector> selectors,
        IEnumerable<IAdditionalInterceptorSelector> additionalSelectors,
        IPropertyInjectorFactory propertyInjectorFactory,
        IAspectCachingProvider cachingProvider,
        BingParameterValidationRegistration parameterValidationRegistration = null)
    {
        _parameterValidationRegistration = parameterValidationRegistration;
        _inner = new InterceptorCollector(
            selectors,
            additionalSelectors,
            propertyInjectorFactory,
            cachingProvider);
    }

    /// <inheritdoc />
    public IEnumerable<IInterceptor> Collect(
        MethodInfo serviceMethod,
        MethodInfo implementationMethod,
        MethodInfo proxyMethod)
    {
        if (IsIgnored(serviceMethod) || IsIgnored(implementationMethod) || IsIgnored(proxyMethod))
            return Array.Empty<IInterceptor>();

        var interceptors = _inner.Collect(serviceMethod, implementationMethod, proxyMethod).ToList();
        if (_parameterValidationRegistration?.Enabled == true &&
            BingImplementationParameterValidationInterceptor.HasImplementationMarkers(serviceMethod, implementationMethod))
            interceptors.Add(new BingImplementationParameterValidationInterceptor());
        return interceptors;
    }

    /// <summary>
    /// 判断方法或其相关接口声明是否带有忽略标记。
    /// </summary>
    /// <param name="method">待检查方法。</param>
    /// <returns>带有标记时返回 <see langword="true" />。</returns>
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
