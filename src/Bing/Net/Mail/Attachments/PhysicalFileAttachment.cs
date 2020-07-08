using System.IO;
using Bing.Net.Mail.Abstractions;

namespace Bing.Net.Mail.Attachments
{
    /// <summary>
    /// 物理文件附件
    /// </summary>
    public class PhysicalFileAttachment : IAttachment
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
        /// 初始化一个<see cref="PhysicalFileAttachment"/>类型的实例
        /// </summary>
        /// <param name="absolutePath">绝对路径</param>
        public PhysicalFileAttachment(string absolutePath)
        {
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"文件未找到：{absolutePath}");
            AbsolutePath = absolutePath;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose() => _stream?.Dispose();

        /// <summary>
        /// 获取文件流
        /// </summary>
        public Stream GetFileStream() => _stream ??= new FileStream(AbsolutePath, FileMode.Open);

        /// <summary>
        /// 获取文件名
        /// </summary>
        public string GetName() => Path.GetFileName(AbsolutePath);
    }
}
