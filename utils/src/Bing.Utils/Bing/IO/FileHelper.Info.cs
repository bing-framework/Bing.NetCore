using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Bing.Helpers;
using Bing.Utils.Files;
using FileInfo = System.IO.FileInfo;

namespace Bing.IO
{
    /// <summary>
    /// 文件操作辅助类 - 信息
    /// </summary>
    public static partial class FileHelper
    {
        #region GetExtension(获取文件扩展名)

        /// <summary>
        /// 获取文件扩展名。例如：a.txt => txt
        /// </summary>
        /// <param name="fileNameWithExtension">文件名。包含扩展名</param>
        public static string GetExtension(string fileNameWithExtension)
        {
            Check.NotNull(fileNameWithExtension, nameof(fileNameWithExtension));

            var lastDotIndex = fileNameWithExtension.LastIndexOf('.');
            if (lastDotIndex < 0)
                return string.Empty;
            return fileNameWithExtension.Substring(lastDotIndex + 1);
        }

        #endregion

        #region GetContentType(根据扩展名获取文件内容类型)

        /// <summary>
        /// 根据扩展名获取文件内容类型
        /// </summary>
        /// <param name="ext">扩展名</param>
        public static string GetContentType(string ext)
        {
            var dict = Const.FileExtensionDict;
            ext = ext.ToLower();
            if (!ext.StartsWith("."))
            {
                ext = "." + ext;
            }

            dict.TryGetValue(ext, out var contentType);
            return contentType;
        }

        #endregion

        #region GetFileSize(获取文件大小)

        /// <summary>
        /// 获取文件大小
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        public static FileSize GetFileSize(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            return GetFileSize(new FileInfo(filePath));
        }

        /// <summary>
        /// 获取文件大小
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <returns></returns>
        public static FileSize GetFileSize(FileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                throw new ArgumentNullException(nameof(fileInfo));
            }

            return new FileSize(fileInfo.Length);
        }

        #endregion

        #region GetVersion(获取文件版本号)

        /// <summary>
        /// 获取文件版本号
        /// </summary>
        /// <param name="fileName">完整文件名</param>
        /// <returns></returns>
        public static string GetVersion(string fileName)
        {
            if (File.Exists(fileName))
            {
                var fvi = FileVersionInfo.GetVersionInfo(fileName);
                return fvi.FileVersion;
            }

            return null;
        }

        #endregion

        #region GetEncoding(获取文件的编码格式)

        /// <summary>
        /// 获取文件编码
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        public static Encoding GetEncoding(string filePath) => GetEncoding(filePath, Encoding.Default);

        /// <summary>
        /// 获取文件编码
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <param name="defaultEncoding">默认编码</param>
        public static Encoding GetEncoding(string filePath, Encoding defaultEncoding)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件\"{filePath}\"不存在");

            var targetEncoding = defaultEncoding;
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4))
            {
                if (fs != null && fs.Length >= 2)
                {
                    var pos = fs.Position;
                    fs.Position = 0;
                    var buffer = new int[4];
                    buffer[0] = fs.ReadByte();
                    buffer[1] = fs.ReadByte();
                    buffer[2] = fs.ReadByte();
                    buffer[3] = fs.ReadByte();
                    fs.Position = pos;

                    // 根据文件流的前4个字节判断Encoding Unicode {0xFF,  0xFE}; BE-Unicode  {0xFE,  0xFF}; UTF8  =  {0xEF,  0xBB,  0xBF};
                    if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                        targetEncoding = Encoding.BigEndianUnicode;
                    if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] != 0xFF)
                        targetEncoding = Encoding.Unicode;
                    if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                        targetEncoding = Encoding.UTF8;
                }
            }

            return targetEncoding;
        }

        /// <summary>
        /// 获取文件流的编码格式
        /// </summary>
        /// <param name="fs">文件流</param>
        public static Encoding GetEncoding(FileStream fs)
        {
            if (fs == null)
                throw new ArgumentNullException(nameof(fs));
            var targetEncoding = Encoding.Default;
            using var br = new BinaryReader(fs, Encoding.Default);
            var buffer = br.ReadBytes(3);
            if (buffer[0] >= 0xEF)
            {
                if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    targetEncoding = Encoding.BigEndianUnicode;
                if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] != 0xFF)
                    targetEncoding = Encoding.Unicode;
                if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    targetEncoding = Encoding.UTF8;
            }
            return targetEncoding;
        }

        #endregion
    }
}
