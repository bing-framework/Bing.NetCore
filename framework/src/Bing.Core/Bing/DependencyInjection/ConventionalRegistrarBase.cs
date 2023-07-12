using Bing.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 常规注册器基类
/// </summary>
public abstract class ConventionalRegistrarBase : IConventionalRegistrar
{
    /// <summary>
    /// 添加程序集
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assembly">程序集</param>
    public virtual void AddAssembly(IServiceCollection services, Assembly assembly)
    {
    }

    /// <summary>
    /// 添加类型数组
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="types">类型数组</param>
    public virtual void AddTypes(IServiceCollection services, params Type[] types)
    {
        foreach (var type in types) 
            AddType(services, type);
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="type">类型</param>
    public abstract void AddType(IServiceCollection services, Type type);

    /// <summary>
    /// 获取指定类型的 <see cref="DependencyAttribute"/>
    /// </summary>
    /// <param name="type">类型</param>
    protected virtual DependencyAttribute GetDependencyAttributeOrNull(Type type)
    {
        return type.GetCustomAttribute<DependencyAttribute>(true);
    }

    /// <summary>
    /// 获取指定类型的 <see cref="ServiceLifetime"/> 生命周期类型
    /// </summary>
    /// <param name="type">依赖注入实现类的类型</param>
    /// <param name="dependencyAttribute">依赖注入行为特性</param>
    protected virtual ServiceLifetime? GetLifetimeOrNull(Type type, DependencyAttribute dependencyAttribute)
    {
        return dependencyAttribute?.Lifetime ?? GetServiceLifetimeFromClassHierarchy(type) ?? GetDefaultLifetimeOrNull(type);
    }

    /// <summary>
    /// 通过类层次结构获取服务 <see cref="ServiceLifetime"/> 生命周期类型
    /// </summary>
    /// <param name="type">依赖注入实现类的类型</param>
    protected virtual ServiceLifetime? GetServiceLifetimeFromClassHierarchy(Type type)
    {
        if (type.IsDeriveClassFrom<ITransientDependency>())
            return ServiceLifetime.Transient;
        if (type.IsDeriveClassFrom<IScopedDependency>())
            return ServiceLifetime.Scoped;
        if (type.IsDeriveClassFrom<ISingletonDependency>())
            return ServiceLifetime.Singleton;
        return null;
    }

    /// <summary>
    /// 获取指定类型的默认 <see cref="ServiceLifetime"/> 生命周期类型
    /// </summary>
    /// <param name="type">依赖注入实现类的类型</param>
    protected virtual ServiceLifetime? GetDefaultLifetimeOrNull(Type type) => null;
}
