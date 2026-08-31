using Bing.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 为约定注册器提供类型遍历和生命周期推断的基类。
/// </summary>
public abstract class ConventionalRegistrarBase : IConventionalRegistrar
{
    /// <inheritdoc />
    /// <remarks>默认实现不执行程序集扫描，派生类可按自身约定重写。</remarks>
    public virtual void AddAssembly(IServiceCollection services, Assembly assembly)
    {
    }

    /// <inheritdoc />
    /// <remarks>默认实现按输入顺序逐个调用 <see cref="AddType"/>。</remarks>
    public virtual void AddTypes(IServiceCollection services, params Type[] types)
    {
        foreach (var type in types) 
            AddType(services, type);
    }

    /// <inheritdoc />
    public abstract void AddType(IServiceCollection services, Type type);

    /// <summary>
    /// 获取指定类型或其继承层级中声明的 <see cref="DependencyAttribute"/>。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>匹配的依赖注入特性；未声明时返回 <c>null</c>。</returns>
    protected virtual DependencyAttribute GetDependencyAttributeOrNull(Type type)
    {
        return type.GetCustomAttribute<DependencyAttribute>(true);
    }

    /// <summary>
    /// 按特性、标记接口和默认规则确定服务生命周期。
    /// </summary>
    /// <param name="type">要确定生命周期的实现类型。</param>
    /// <param name="dependencyAttribute">可选的依赖注入特性。</param>
    /// <returns>已确定的服务生命周期；无法确定时返回 <c>null</c>。</returns>
    protected virtual ServiceLifetime? GetLifetimeOrNull(Type type, DependencyAttribute dependencyAttribute)
    {
        return dependencyAttribute?.Lifetime ?? GetServiceLifetimeFromClassHierarchy(type) ?? GetDefaultLifetimeOrNull(type);
    }

    /// <summary>
    /// 从实现的依赖标记接口推断服务生命周期。
    /// </summary>
    /// <param name="type">要检查的实现类型。</param>
    /// <returns>由标记接口推断出的生命周期；未实现标记接口时返回 <c>null</c>。</returns>
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
    /// 获取指定类型的默认服务生命周期。
    /// </summary>
    /// <param name="type">要检查的实现类型。</param>
    /// <returns>默认服务生命周期；当前基类未提供默认值时返回 <c>null</c>。</returns>
    protected virtual ServiceLifetime? GetDefaultLifetimeOrNull(Type type) => null;
}
