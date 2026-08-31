namespace Bing.Core.Modularity;

/// <summary>
/// 依赖类型提供程序
/// </summary>
public interface IDependedTypesProvider
{
    /// <summary>
    /// 获取依赖类型
    /// </summary>
    /// <returns>由当前提供程序声明的依赖类型数组。</returns>
    Type[] GetDependedTypes();
}
