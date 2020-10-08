using System.IO;
using System.Text;
using Bing.Extensions;

namespace Bing.IO
{
    /// <summary>
    /// 文件操作帮助类 - 写入
    /// </summary>
    public static partial class FileHelper
    {
        #region SaveFile(保存内容到文件)

        /// <summary>
        /// 保存内容到文件
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="content">数据</param>
        public static bool SaveFile(string filePath, string content)
        {
            var encoding = Encoding.GetEncoding("gb2312");
            return SaveFile(filePath, content, encoding);
        }

        /// <summary>
        /// 保存内容到文件
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="content">数据</param>
        /// <param name="encoding">字符编码</param>
        public static bool SaveFile(string filePath, string content, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;
            if (encoding == null)
                encoding = Encoding.UTF8;
            try
            {
                DirectoryHelper.CreateIfNotExists(DirectoryHelper.GetDirectoryPath(filePath));
                if (File.Exists(filePath))
                {
                    File.WriteAllText(filePath, content, encoding);
                }
                else
                {
                    var sw = new StreamWriter(filePath, false, encoding);
                    sw.Write(content);
                    sw.Flush();
                    sw.Close();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Write(将内容写入文件)

        /// <summary>
        /// 将内容写入文件，文件不存在则创建
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="content">数据</param>
        public static void Write(string filePath, string content)
        {
            Write(filePath, FileHelper.ToBytes(content.SafeString()));
        }

        /// <summary>
        /// 将内容写入文件，文件不存在则创建
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="bytes">数据</param>
        public static void Write(string filePath, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
            if (bytes == null)
                return;
            File.WriteAllBytes(filePath, bytes);
        }

        #endregion
    }
}
