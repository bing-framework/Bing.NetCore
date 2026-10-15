namespace Bing.Core.Modularity;

/// <summary>
/// 构建并校验模块图的加载器契约。
/// </summary>
/// <remarks>
/// 注册应用前可按实例注册自定义加载器，以替换默认的模块发现和排序实现。
/// </remarks>
public interface IBingModuleLoader
{
    /// <summary>
    /// 创建并排序模块描述符。
    /// </summary>
    /// <param name="roots">模块图的根类型。</param>
    /// <param name="excludedModules">禁止加载的模块类型。</param>
    /// <returns>按执行顺序排列的模块描述符集合。</returns>
    /// <remarks>
    /// 加载失败时，实现必须释放已经创建的模块实例。
    /// </remarks>
    IReadOnlyList<BingModuleDescriptor> LoadModules(IEnumerable<Type> roots, IEnumerable<Type> excludedModules);
}
