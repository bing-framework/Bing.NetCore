using System;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 文件信息(<see cref="FileInfo"/>) 扩展
    /// </summary>
    public static class FileInfoExtensions
    {
        #region CompareTo(比较文件)

        /// <summary>
        /// 比较文件
        /// </summary>
        /// <param name="file1">文件1</param>
        /// <param name="file2">文件2</param>
        /// <returns></returns>
        public static bool CompareTo(this FileInfo file1, FileInfo file2)
        {
            if (file1 == null || !file1.Exists)
            {
                throw new ArgumentNullException(nameof(file1));
            }

            if (file2 == null || !file2.Exists)
            {
                throw new ArgumentNullException(nameof(file2));
            }

            if (file1.Length != file2.Length)
            {
                return false;
            }

            return file1.Read().Equals(file2.Read());
        }

        #endregion

        #region Read(读取文件并转换为字符串)

        /// <summary>
        /// 读取文件并转换为字符串
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns></returns>
        public static string Read(this FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }
            
            if (file.Exists == false)
            {
                return string.Empty;
            }
            
            using (var reader = file.OpenText())
            {
                return reader.ReadToEnd();
            }
        }

        #endregion

        #region ReadBinary(读取文件并转换为二进制数组)

        /// <summary>
        /// 读取文件并转换为二进制数组
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns></returns>
        public static byte[] ReadBinary(this FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (file.Exists == false)
            {
                return new byte[0];
            }

            using (var reader = file.OpenRead())
            {
                return reader.ReadAllBytes();
            }
        }

        #endregion
    }
}
