using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Bing.Utils.Files;
using Bing.Utils.Helpers;
using FileInfo = System.IO.FileInfo;

namespace Bing.Utils.IO
{
    /// <summary>
    /// 文件操作辅助类 - 信息
    /// </summary>
    public static partial class FileUtil
    {
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

            return GetFileSize(new System.IO.FileInfo(filePath));
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
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(fileName);
                return fvi.FileVersion;
            }

            return null;
        }

        #endregion

        #region GetFileMd5(获取文件的MD5值)

        /// <summary>
        /// 获取文件的MD5值
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns></returns>
        public static string GetFileMd5(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            const int bufferSize = 1024 * 1024;
            byte[] buffer = new byte[bufferSize];

            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            md5.Initialize();

            long offset = 0;
            while (offset < fs.Length)
            {
                long readSize = bufferSize;
                if (offset + readSize > fs.Length)
                {
                    readSize = fs.Length - offset;
                }

                fs.Read(buffer, 0, (int)readSize);
                if (offset + readSize < fs.Length)
                {
                    md5.TransformBlock(buffer, 0, (int)readSize, buffer, 0);
                }
                else
                {
                    md5.TransformFinalBlock(buffer, 0, (int)readSize);
                }

                offset += bufferSize;
            }

            fs.Close();
            byte[] result = md5.Hash;
            md5.Clear();
            StringBuilder sb = new StringBuilder();
            foreach (var b in result)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
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
            Encoding targetEncoding = defaultEncoding;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4))
            {
                if (fs != null && fs.Length >= 2)
                {
                    long pos = fs.Position;
                    fs.Position = 0;
                    int[] buffer = new int[4];
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
    }
}
