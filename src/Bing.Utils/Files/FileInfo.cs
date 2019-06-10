using Bing.Utils.Extensions;

namespace Bing.Utils.Files
{
    /// <summary>
    /// 文件信息
    /// </summary>
    public class FileInfo
    {
        /// <summary>
        /// 文件标识
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// 存储文件名
        /// </summary>
        public string SaveName { get; set; }

        /// <summary>
        /// 扩展名
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// 文件大小
        /// </summary>
        public FileSize Size { get; }

        /// <summary>
        /// 文件的MD5值
        /// </summary>
        public string Md5 { get; set; }

        /// <summary>
        /// 访问地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 完整存储路径。不包含根路径
        /// </summary>
        public string FullPath => System.IO.Path.Combine(Path, SaveName);

        /// <summary>
        /// 初始化一个<see cref="FileInfo"/>类型的实例
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="size">文件大小</param>
        /// <param name="fileName">文件名</param>
        /// <param name="id">文件标识</param>
        public FileInfo(string path, long? size, string fileName = null, string id = null)
        {
            Path = path;
            Size = new FileSize(size.SafeValue());
            Extension = GetExtension(path, fileName);
            FileName = string.IsNullOrWhiteSpace(fileName) ? System.IO.Path.GetFileName(path) : fileName;
            Id = id;
        }

        /// <summary>
        /// 获取扩展名
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="fileName">文件名</param>
        /// <returns></returns>
        private string GetExtension(string path, string fileName)
        {
            var extension = System.IO.Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension= System.IO.Path.GetExtension(fileName);
            }

            return extension?.TrimStart('.');
        }
    }
}
