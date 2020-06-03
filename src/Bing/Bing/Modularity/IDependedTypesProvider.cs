using System;

namespace Bing.Modularity
{
    /// <summary>
    /// 依赖类型提供程序
    /// </summary>
    public interface IDependedTypesProvider
    {
        /// <summary>
        /// 获取依赖类型数组
        /// </summary>
        Type[] GetDependedTypes();
    }
}
