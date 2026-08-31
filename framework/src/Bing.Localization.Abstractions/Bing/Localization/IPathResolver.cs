using System.Globalization;
using System.Reflection;

namespace Bing.Localization;

/// <summary>
/// 路径解析器
/// </summary>
public interface IPathResolver
{
    /// <summary>
    /// 获取根命名空间
    /// </summary>
    /// <param name="assembly">资源类型所在的程序集</param>
    /// <returns>程序集对应的根命名空间。</returns>
    string GetRootNamespace(Assembly assembly);

    /// <summary>
    /// 获取资源根目录路径
    /// </summary>
    /// <param name="assembly">资源类型所在的程序集</param>
    /// <param name="rootPath">配置的根路径</param>
    /// <returns>本地化资源根目录的绝对路径。</returns>
    string GetResourcesRootPath(Assembly assembly, string rootPath);

    /// <summary>
    /// 获取资源基名称
    /// </summary>
    /// <param name="assembly">资源类型所在的程序集</param>
    /// <param name="typeFullName">资源类型完全限定名</param>
    /// <returns>指定资源类型对应的资源基名称。</returns>
    string GetResourcesBaseName(Assembly assembly, string typeFullName);

    /// <summary>
    /// 获取Json资源文件绝对路径
    /// </summary>
    /// <param name="rootPath">资源根目录路径</param>
    /// <param name="baseName">资源基名称</param>
    /// <param name="culture">区域文化</param>
    /// <returns>指定区域文化对应的 JSON 资源文件绝对路径。</returns>
    string GetJsonResourcePath(string rootPath, string baseName, CultureInfo culture);
}
