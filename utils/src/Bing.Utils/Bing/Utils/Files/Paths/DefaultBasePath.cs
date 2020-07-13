namespace Bing.Utils.Files.Paths
{
    /// <summary>
    /// 默认基路径
    /// </summary>
    public class DefaultBasePath : IBasePath
    {
        /// <summary>
        /// 基路径
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// 初始化一个<see cref="DefaultBasePath"/>类型的实例
        /// </summary>
        /// <param name="path">基路径</param>
        public DefaultBasePath(string path) => _path = path;

        /// <summary>
        /// 获取基路径
        /// </summary>
        public string GetPath() => _path;
    }
}
