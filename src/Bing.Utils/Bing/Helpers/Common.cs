using System;
using System.IO;
using Bing.Utils.Helpers;

namespace Bing.Helpers
{
    /// <summary>
    /// 常用公共操作
    /// </summary>
    public static class Common
    {
        #region GetType(获取类型)

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public static Type GetType<T>() => GetType(typeof(T));

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <param name="type">类型</param>
        public static Type GetType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

        #endregion

        #region Line(换行符)

        /// <summary>
        /// 换行符
        /// </summary>
        public static string Line => Environment.NewLine;

        #endregion

        #region GetPhysicalPath(获取物理路径)

        /// <summary>
        /// 获取物理路径
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        public static string GetPhysicalPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;
            var rootPath = Web.RootPath;
            if (string.IsNullOrWhiteSpace(rootPath))
                return Path.GetFullPath(relativePath);
            return $"{Web.RootPath}\\{relativePath.Replace("/", "\\").TrimStart('\\')}";
        }

        #endregion

        #region GetWebRootPath(获取wwwroot路径)

        /// <summary>
        /// 获取wwwroot路径
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        public static string GetWebRootPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;
            var rootPath = Web.WebRootPath;
            if (string.IsNullOrWhiteSpace(rootPath))
                return Path.GetFullPath(relativePath);
            return $"{Web.WebRootPath}\\{relativePath.Replace("/", "\\").TrimStart('\\')}";
        }

        #endregion

        #region Swap(交换值)

        /// <summary>
        /// 交换值。交换两个提供的变量中的值
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="a">变量A</param>
        /// <param name="b">变量B</param>
        public static void Swap<T>(ref T a, ref T b)
        {
            var swap = a;
            a = b;
            b = swap;
        }

        #endregion

    }
}
