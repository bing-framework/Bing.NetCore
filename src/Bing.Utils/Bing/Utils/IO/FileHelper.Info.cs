using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Bing.Helpers;
using Bing.Utils.Files;
using FileInfo = System.IO.FileInfo;

namespace Bing.Utils.IO
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
        /// <returns></returns>
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

        #region GetEncoding(获取文件编码)

        /// <summary>
        /// 获取文件编码
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <returns></returns>
        public static Encoding GetEncoding(string filePath)
        {
            return GetEncoding(filePath, Encoding.Default);
        }

        /// <summary>
        /// 获取文件编码
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <param name="defaultEncoding">默认编码</param>
        /// <returns></returns>
        public static Encoding GetEncoding(string filePath, Encoding defaultEncoding)
        {
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

                    if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    {
                        targetEncoding = Encoding.BigEndianUnicode;
                    }

                    if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    {
                        targetEncoding = Encoding.Unicode;
                    }

                    if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    {
                        targetEncoding = Encoding.UTF8;
                    }
                }
            }

            return targetEncoding;
        }

        #endregion

        #region GetMd5(获取文件的MD5值)

        /// <summary>
        /// 获取文件的MD5值
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns></returns>
        public static string GetMd5(string file)
        {
            return HashFile(file, "md5");
        }

        /// <summary>
        /// 计算文件的哈希值
        /// </summary>
        /// <param name="file">文件</param>
        /// <param name="algName">算法名。例如：md5,sha1</param>
        /// <returns></returns>
        private static string HashFile(string file, string algName)
        {
            if (!File.Exists(file))
            {
                return string.Empty;
            }

            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var bytes = HashData(fs, algName);
                return ToHexString(bytes);
            }
        }

        /// <summary>
        /// 计算哈希值
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="algName">算法名。例如：md5,sha1</param>
        /// <returns></returns>
        private static byte[] HashData(Stream stream, string algName)
        {
            if (string.IsNullOrWhiteSpace(algName))
            {
                throw new ArgumentNullException(nameof(algName));
            }

            HashAlgorithm algorithm;
            if (string.Compare(algName, "sha1", StringComparison.OrdinalIgnoreCase) == 0)
            {
                algorithm = SHA1.Create();
            }
            else if (string.Compare(algName, "md5", StringComparison.OrdinalIgnoreCase) == 0)
            {
                algorithm = MD5.Create();
            }
            else
            {
                throw new ArgumentException($"{nameof(algName)} 只能使用 sha1 或 md5.");
            }

            var bytes = algorithm.ComputeHash(stream);
            algorithm.Dispose();
            return bytes;
        }

        /// <summary>
        /// 将字节数组转换为16进制表示的字符在
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        private static string ToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        #endregion

        #region GetSha1(获取文件的SHA1值)

        /// <summary>
        /// 获取文件的SHA1值
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns></returns>
        public static string GetSha1(string file)
        {
            return HashFile(file, "sha1");
        }

        #endregion
    }
}
