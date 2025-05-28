using AspectCore.Configuration;
using AspectCore.DynamicProxy;
using AspectCore.Extensions.AspectScope;
using AspectCore.Extensions.DependencyInjection;
using Bing.Aspects;
using Bing.Exceptions.Prompts;
using Bing.Extensions;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// AspectCore 扩展
/// </summary>
public static class AspectCoreExtensions
{
    /// <summary>
    /// 启用Aop
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configAction">Aop配置</param>
    /// <remarks>
    /// 默认不开启参数拦截 EnableParameterAspect()，该方法将会导致异常不能更好的定位
    /// </remarks>
    public static void EnableAop(this IServiceCollection services, Action<IAspectConfiguration> configAction = null)
    {
        ExceptionPrompt.AddPrompt(new AspectExceptionPrompt());
        services.ConfigureDynamicProxy(config =>
        {
            //config.EnableParameterAspect();// 启用参数拦截，会导致异常不能很好的定位
            config.NonAspectPredicates.Add(t => Reflections.GetTopBaseType(t.DeclaringType).SafeString() == "Microsoft.EntityFrameworkCore.DbContext");
            config.IgnoreAspectInterfaces();
            configAction?.Invoke(config);
        });
        services.RegisterAspectScoped();
    }

    /// <summary>
    /// 注册拦截作用域
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void RegisterAspectScoped(this IServiceCollection services)
    {
        services.AddScoped<IAspectScheduler, ScopeAspectScheduler>();
        services.AddScoped<IAspectBuilderFactory, ScopeAspectBuilderFactory>();
        services.AddScoped<IAspectContextFactory, ScopeAspectContextFactory>();
    }

    /// <summary>
    /// 忽略拦截接口
    /// </summary>
    /// <param name="configuration"></param>
    public static void IgnoreAspectInterfaces(this IAspectConfiguration configuration)
    {
        var interfaces = AssemblyManager.FindTypes(x => x.IsInterface && x.HasAttribute<IgnoreAspectAttribute>()).Distinct().ToArray();
        foreach (var @interface in interfaces)
            configuration.NonAspectPredicates.Add(m => m.DeclaringType == @interface);
    }

    /// <summary>
    /// 是否创建代理
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="isEnableAopProxy">是否启用IAopProxy接口标记</param>
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
