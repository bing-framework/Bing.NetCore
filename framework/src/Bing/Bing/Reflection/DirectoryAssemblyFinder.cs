using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Bing.Reflection
{
    /// <summary>
    /// 目录程序集查找器
    /// </summary>
    public class DirectoryAssemblyFinder : IAssemblyFinder
    {
        /// <summary>
        /// 程序集缓存字典
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly ConcurrentDictionary<string, Assembly[]> AssemblyCacheDict;

        /// <summary>
        /// 目录路径
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static DirectoryAssemblyFinder() => AssemblyCacheDict = new ConcurrentDictionary<string, Assembly[]>();

        /// <summary>
        /// 初始化一个<see cref="DirectoryAssemblyFinder"/>类型的实例
        /// </summary>
        /// <param name="path">目录路径</param>
        public DirectoryAssemblyFinder(string path) => _path = path;

        /// <summary>
        /// 查找指定条件的项
        /// </summary>
        /// <param name="predicate">筛选条件</param>
        /// <param name="fromCache">是否来自缓存</param>
        public Assembly[] Find(Func<Assembly, bool> predicate, bool fromCache = false) => FindAll(fromCache).Where(predicate).ToArray();

        /// <summary>
        /// 查找所有项
        /// </summary>
        /// <param name="fromCache">是否来自缓存</param>
        public Assembly[] FindAll(bool fromCache = false)
        {
            if (fromCache && AssemblyCacheDict.ContainsKey(_path))
                return AssemblyCacheDict[_path];
            var files = Directory.GetFiles(_path, "*.dll", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(_path, "*.exe", SearchOption.TopDirectoryOnly))
                .ToArray();
            var assemblies = files.Select(Assembly.LoadFrom).Distinct().ToArray();
            AssemblyCacheDict[_path] = assemblies;
            return assemblies;
        }
    }
}
