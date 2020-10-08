using System.IO;
using System.Text;
using System.Threading.Tasks;
using Bing.Helpers;

namespace Bing.IO
{
    /// <summary>
    /// 文件操作帮助类 - 读取
    /// </summary>
    public static partial class FileHelper
    {
        #region ReadAllText(读取文件所有文本)

        /// <summary>
        /// 读取文件所有文本
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static async Task<string> ReadAllTextAsync(string filePath)
        {
            Check.NotNull(filePath, nameof(filePath));
            using var reader = File.OpenText(filePath);
            return await reader.ReadToEndAsync();
        }

        #endregion

        #region ReadAllBytes(读取文件所有字节)

        /// <summary>
        /// 读取文件所有字节
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static async Task<byte[]> ReadAllBytesAsync(string filePath)
        {
            Check.NotNull(filePath, nameof(filePath));
            using var stream = File.Open(filePath, FileMode.Open);
            var result = new byte[stream.Length];
            await stream.ReadAsync(result, 0, (int)stream.Length);
            return result;
        }

        #endregion

        #region Read(读取文件到字符串)

        /// <summary>
        /// 读取文件到字符串
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        public static string Read(string filePath) => Read(filePath, Encoding.UTF8);

        /// <summary>
        /// 读取文件到字符串
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="encoding">字符编码</param>
        public static string Read(string filePath, Encoding encoding)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
            if (!File.Exists(filePath))
                return string.Empty;
            using var reader = new StreamReader(filePath, encoding);
            return reader.ReadToEnd();
        }

        #endregion

        #region ReadToBytes(将文件读取到字节流中)

        /// <summary>
        /// 将文件读取到字节流中
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        public static byte[] ReadToBytes(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            return ReadToBytes(new FileInfo(filePath));
        }

        /// <summary>
        /// 将文件读取到字节流中
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        public static byte[] ReadToBytes(FileInfo fileInfo)
        {
            if (fileInfo == null)
                return null;
            var fileSize = (int)fileInfo.Length;
            using var reader = new BinaryReader(fileInfo.Open(FileMode.Open));
            return reader.ReadBytes(fileSize);
        }

        #endregion
    }
}
