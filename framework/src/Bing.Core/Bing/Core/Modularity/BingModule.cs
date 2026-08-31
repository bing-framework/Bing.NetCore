using Bing.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 为 Bing 模块提供默认生命周期行为的基类。
/// </summary>
public abstract class BingModule : IBingModule
{
    /// <inheritdoc />
    public virtual ModuleLevel Level => ModuleLevel.Business;

    /// <inheritdoc />
    public virtual int Order => 0;

    /// <inheritdoc />
    public virtual bool Enabled { get; protected set; }

    /// <inheritdoc />
    /// <remarks>默认实现不注册额外服务，直接返回 <paramref name="services"/>。</remarks>
    public virtual IServiceCollection AddServices(IServiceCollection services) => services;

    /// <inheritdoc />
    /// <remarks>默认实现仅将 <see cref="Enabled"/> 标记为 <c>true</c>。</remarks>
    public virtual void UseModule(IServiceProvider provider) => Enabled = true;

    /// <summary>
    /// 获取指定模块直接和间接依赖的模块类型。
    /// </summary>
    /// <param name="moduleType">要检查的模块类型；为空时使用当前实例的类型。</param>
    /// <returns>去重后的依赖模块类型数组。</returns>
    internal Type[] GetDependModuleTypes(Type moduleType = null)
    {
        moduleType ??= GetType();
        var dependAttrs = moduleType.GetAttributes<DependsOnModuleAttribute>(true).ToList();
        if (dependAttrs.Count == 0)
            return Type.EmptyTypes;
        
        var dependTypes = new HashSet<Type>();
        foreach (var dependAttr in dependAttrs)
        {
            if (dependAttr.DependedModuleTypes?.Length > 0)
            {
                foreach (var type in dependAttr.DependedModuleTypes)
                {
                    dependTypes.Add(type);
                    foreach (var subType in GetDependModuleTypes(type)) 
                        dependTypes.Add(subType);
                }
            }
        }
        return dependTypes.ToArray();
    }

    #region 辅助方法

    /// <summary>
    /// 判断给定类型是否为可实例化的 <see cref="IBingModule"/> 模块类型。
    /// </summary>
    /// <param name="type">待验证的类型。</param>
    /// <returns>有效的非抽象、非泛型模块类型时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsBingModule(Type type)
    {
        if (type == null)
            return false;
        var typeInfo = type.GetTypeInfo();
        return typeInfo.IsClass &&
               !typeInfo.IsAbstract &&
               !typeInfo.IsGenericType &&
               typeof(IBingModule).GetTypeInfo().IsAssignableFrom(type);
    }

    /// <summary>
    /// 验证模块类型是否为可实例化的 <see cref="IBingModule"/>。
    /// </summary>
    /// <param name="moduleType">待验证的模块类型。</param>
    /// <exception cref="ArgumentException">类型不是有效模块类型时抛出。</exception>
    internal static void CheckBingModuleType(Type moduleType)
    {
        if (!IsBingModule(moduleType))
            throw new ArgumentException("Given type is not an Bing Module: " + moduleType.AssemblyQualifiedName);
    }

    #endregion

}
