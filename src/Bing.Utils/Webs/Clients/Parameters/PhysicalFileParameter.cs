using System.IO;

namespace Bing.Utils.Webs.Clients.Parameters
{
    /// <summary>
    /// 物理文件参数
    /// </summary>
    public class PhysicalFileParameter : IFileParameter
    {
        /// <summary>
        /// 文件流
        /// </summary>
        private FileStream _stream;

        /// <summary>
        /// 绝对路径
        /// </summary>
        public string AbsolutePath { get; }

        /// <summary>
        /// 参数名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 初始化一个<see cref="PhysicalFileParameter"/>类型的实例
        /// </summary>
        /// <param name="absolutePath">绝对路径</param>
        public PhysicalFileParameter(string absolutePath) : this(absolutePath, "files") { }

        /// <summary>
        /// 初始化一个<see cref="PhysicalFileParameter"/>类型的实例
        /// </summary>
        /// <param name="absolutePath">绝对路径</param>
        /// <param name="name">参数名</param>
        public PhysicalFileParameter(string absolutePath, string name)
        {
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"文件未找到：{absolutePath}");
            AbsolutePath = absolutePath;
            Name = name;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose() => _stream?.Dispose();

        /// <summary>
        /// 获取文件流
        /// </summary>
        public Stream GetFileStream() => _stream ?? (_stream = new FileStream(AbsolutePath, FileMode.Open));

        /// <summary>
        /// 获取文件名
        /// </summary>
        public string GetFileName() => Path.GetFileName(AbsolutePath);

        /// <summary>
        /// 获取参数名
        /// </summary>
        public string GetName() => Name;
    }
}
