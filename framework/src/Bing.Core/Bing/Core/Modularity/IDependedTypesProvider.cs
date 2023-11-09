namespace Bing.Core.Modularity;

/// <summary>
/// 依赖类型提供程序
/// </summary>
public interface IDependedTypesProvider
{
    /// <summary>
    /// 获取依赖类型
    /// </summary>
    Type[] GetDependedTypes();
}
