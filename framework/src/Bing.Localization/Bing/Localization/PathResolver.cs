using Bing.Extensions;
using Bing.Text;
using Bing.Helpers;

namespace Bing.Localization;

/// <summary>
/// 路径解析器
/// </summary>
public class PathResolver : IPathResolver
{
    /// <summary>
    /// 获取根命名空间
    /// </summary>
    /// <param name="assembly">资源类型所在的程序集</param>
    /// <returns>程序集配置的根命名空间；未配置时返回程序集名称。</returns>
    public string GetRootNamespace(Assembly assembly)
    {
        assembly.CheckNull(nameof(assembly));
        var attribute = assembly.GetCustomAttribute<RootNamespaceAttribute>();
        return attribute == null ? assembly.GetName().Name : attribute.RootNamespace;
    }

    /// <summary>
    /// 获取资源根目录路径
    /// </summary>
    /// <param name="assembly">资源类型所在的程序集</param>
    /// <param name="rootPath">配置的根路径</param>
    /// <returns>程序集配置的资源根路径；未配置时返回传入根路径。</returns>
    public string GetResourcesRootPath(Assembly assembly, string rootPath)
    {
        if (assembly == null)
            return rootPath;
        var attribute = assembly.GetCustomAttribute<ResourceLocationAttribute>();
        return attribute == null ? rootPath : attribute.ResourceLocation;
    }

    /// <summary>
    /// 获取资源基名称
    /// </summary>
    /// <param name="assembly">资源类型所在的程序集</param>
    /// <param name="typeFullName">资源类型完全限定名</param>
    /// <returns>资源基名称。</returns>
    public string GetResourcesBaseName(Assembly assembly, string typeFullName)
    {
        var rootNamespace = GetRootNamespace(assembly);
        return typeFullName.RemoveStart($"{rootNamespace}.");
    }

    /// <summary>
    /// 获取Json资源文件绝对路径
    /// </summary>
    /// <param name="rootPath">资源根目录路径</param>
    /// <param name="baseName">资源基名称</param>
    /// <param name="culture">区域文化</param>
    /// <returns>对应区域文化和资源基名称的 JSON 文件绝对路径。</returns>
    public string GetJsonResourcePath(string rootPath, string baseName, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(baseName))
            return Path.Combine(Common.ApplicationBaseDirectory, rootPath, $"{culture.Name}.json");
        baseName = FixInnerClassPath(baseName);
        return Path.Combine(Common.ApplicationBaseDirectory, rootPath, $"{baseName}.{culture.Name}.json");
    }

    /// <summary>
    /// 修复内部类分隔符+
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>将内部类分隔符转换为路径使用的点号后的路径。</returns>
    private string FixInnerClassPath(string path)
    {
        const char innerClassSeparator = '+';
        return path.Contains(innerClassSeparator) ? path.Replace(innerClassSeparator, '.') : path;
    }
}
