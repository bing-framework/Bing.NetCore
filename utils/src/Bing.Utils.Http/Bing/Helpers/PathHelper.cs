using System.IO;

namespace Bing.Helpers
{
    /// <summary>
    /// 路径 操作
    /// </summary>
    public static partial class PathHelper
    {
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
    }
}
