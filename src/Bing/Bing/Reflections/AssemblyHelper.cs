using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Bing.Reflections
{
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
        public static List<Assembly> LoadAssemblies(string folderPath, SearchOption searchOption) =>
            GetAssemblyFiles(folderPath, searchOption)
                .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
                .ToList();

        /// <summary>
        /// 获取程序集文件列表
        /// </summary>
        /// <param name="folderPath">目录路径</param>
        /// <param name="searchOption">查询选项</param>
        public static IEnumerable<string> GetAssemblyFiles(string folderPath, SearchOption searchOption) =>
            Directory.EnumerateFiles(folderPath, "*.*", searchOption)
                .Where(s => s.EndsWith(".dll") || s.EndsWith(".exe"));

        /// <summary>
        /// 获取程序集中所有类型
        /// </summary>
        /// <param name="assembly">程序集</param>
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
}
