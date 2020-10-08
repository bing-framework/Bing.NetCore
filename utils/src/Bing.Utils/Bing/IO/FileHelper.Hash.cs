using System;
using System.IO;
using System.Security.Cryptography;

namespace Bing.IO
{
    /// <summary>
    /// 文件操作辅助类 - MD5
    /// </summary>
    public static partial class FileHelper
    {
        #region GetMd5(获取文件的MD5值)

        /// <summary>
        /// 获取文件的MD5值
        /// </summary>
        /// <param name="file">文件</param>
        public static string GetMd5(string file) => HashFile(file, "md5");

        /// <summary>
        /// 计算文件的哈希值
        /// </summary>
        /// <param name="file">文件</param>
        /// <param name="algName">算法名。例如：md5,sha1</param>
        /// <returns>哈希值16进制字符串</returns>
        private static string HashFile(string file, string algName)
        {
            if (!File.Exists(file))
                return string.Empty;
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            var bytes = HashData(fs, algName);
            return ToHexString(bytes);
        }

        /// <summary>
        /// 计算哈希值
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="algName">算法名。例如：md5,sha1</param>
        /// <returns>哈希值字节数组</returns>
        private static byte[] HashData(Stream stream, string algName)
        {
            if (string.IsNullOrWhiteSpace(algName))
                throw new ArgumentNullException(nameof(algName));
            if(string.IsNullOrWhiteSpace(algName))
                throw new ArgumentNullException(nameof(algName));

            HashAlgorithm algorithm;
            if (string.Compare(algName, "sha1", StringComparison.OrdinalIgnoreCase) == 0)
                algorithm = SHA1.Create();
            else if (string.Compare(algName, "md5", StringComparison.OrdinalIgnoreCase) == 0)
                algorithm = MD5.Create();
            else
                throw new ArgumentException($"{nameof(algName)} 只能使用 sha1 或 md5.");

            var bytes = algorithm.ComputeHash(stream);
            algorithm.Dispose();
            return bytes;
        }

        /// <summary>
        /// 将字节数组转换为16进制表示的字符在
        /// </summary>
        /// <param name="bytes">字节数组</param>
        private static string ToHexString(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

        #endregion

        #region GetSha1(获取文件的SHA1值)

        /// <summary>
        /// 获取文件的SHA1值
        /// </summary>
        /// <param name="file">文件</param>
        public static string GetSha1(string file) => HashFile(file, "sha1");

        #endregion
    }
}
