using System.Runtime.Loader;

namespace Bing.Reflection;

/// <summary>
/// 程序集操作辅助类
/// </summary>
internal static class AssemblyHelper
{
    /// <summary>
    /// 加载程序集列表
    /// </summary>
    /// <param name="folderPath">目录路径</param>
    /// <param name="searchOption">查询选项</param>
    /// <returns>已加载的程序集列表。</returns>
    public static List<Assembly> LoadAssemblies(string folderPath, SearchOption searchOption) =>
        GetAssemblyFiles(folderPath, searchOption)
            .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
            .ToList();

    /// <summary>
    /// 获取程序集文件列表
    /// </summary>
    /// <param name="folderPath">目录路径</param>
    /// <param name="searchOption">查询选项</param>
    /// <returns>目录中的程序集文件路径集合。</returns>
    public static IEnumerable<string> GetAssemblyFiles(string folderPath, SearchOption searchOption) =>
        Directory.EnumerateFiles(folderPath, "*.*", searchOption)
            .Where(s => s.EndsWith(".dll") || s.EndsWith(".exe"));

    /// <summary>
    /// 获取程序集中所有类型
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>程序集中的类型集合。</returns>
    public static IReadOnlyList<Type> GetAllTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
    }
}
