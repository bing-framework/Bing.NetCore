using Bing.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// Bing 模块基类，所有模块应继承此类。
/// </summary>
public abstract class BingModule : IBingModule
{
    /// <summary>
    /// 模块级别。级别越小越先启动
    /// </summary>
    public virtual ModuleLevel Level => ModuleLevel.Business;

    /// <summary>
    /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
    /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
    /// </summary>
    public virtual int Order => 0;

    /// <summary>
    /// 是否已启用
    /// </summary>
    public virtual bool Enabled { get; protected set; }

    /// <summary>
    /// 添加服务。将模块服务添加到依赖注入服务容器中
    /// </summary>
    /// <param name="services">服务集合</param>
    public virtual IServiceCollection AddServices(IServiceCollection services) => services;

    /// <summary>
    /// 应用模块服务
    /// </summary>
    /// <param name="provider">服务提供程序</param>
    public virtual void UseModule(IServiceProvider provider) => Enabled = true;

    /// <summary>
    /// 获取当前模块的依赖模块类型
    /// </summary>
    /// <param name="moduleType">模块类型</param>
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
    /// 判断给定类型是否是 <see cref="IBingModule"/> 模块类型。
    /// </summary>
    /// <param name="type">待验证的类型</param>
    /// <returns>如果是有效的 Bing 模块类型，则返回 <c>true</c>，否则返回 <c>false</c></returns>
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
    /// 检查模块类型是否<see cref="IBingModule"/>类型
    /// </summary>
    /// <param name="moduleType">模块类型</param>
    internal static void CheckBingModuleType(Type moduleType)
    {
        if (!IsBingModule(moduleType))
            throw new ArgumentException("Given type is not an Bing Module: " + moduleType.AssemblyQualifiedName);
    }

    #endregion

}
