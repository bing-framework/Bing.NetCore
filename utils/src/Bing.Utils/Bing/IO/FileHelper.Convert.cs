using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Bing.IO
{
    /// <summary>
    /// 文件操作辅助类 - 转换
    /// </summary>
    public static partial class FileHelper
    {
        #region ToString(转换成字符串)

        /// <summary>
        /// 字节数组转换成字符串
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string ToString(byte[] data, Encoding encoding = null)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            return encoding.GetString(data);
        }

        /// <summary>
        /// 流转换成字符串
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="encoding">字符串编码</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="isCloseStream">读取完成是否释放流，默认为true</param>
        /// <returns></returns>
        public static string ToString(Stream stream, Encoding encoding = null, int bufferSize = 1024 * 2,
            bool isCloseStream = true)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            if (stream.CanRead == false)
            {
                return string.Empty;
            }

            using (var reader = new StreamReader(stream, encoding, true, bufferSize, !isCloseStream))
            {
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                var result = reader.ReadToEnd();
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                return result;
            }
        }

        /// <summary>
        /// 流转换成字符串
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="encoding">字符串编码</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="isCloseStream">读取完成是否释放流，默认为true</param>
        /// <returns></returns>
        public static async Task<string> ToStringAsync(Stream stream, Encoding encoding = null,
            int bufferSize = 1024 * 2,
            bool isCloseStream = true)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            if (stream.CanRead == false)
            {
                return string.Empty;
            }

            using (var reader = new StreamReader(stream, encoding, true, bufferSize, !isCloseStream))
            {
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                var result = await reader.ReadToEndAsync();
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                return result;
            }
        }

        #endregion

        #region ToStream(转换成流)

        /// <summary>
        /// 字符串转换成流
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static Stream ToStream(string data, Encoding encoding = null)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return Stream.Null;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            return new MemoryStream(ToBytes(data, encoding));
        }

        #endregion

        #region ToBytes(转换成字节数组)

        /// <summary>
        /// 字符串转换为字节数组
        /// </summary>
        /// <param name="data">数据。默认字符编码：utf-8</param>
        public static byte[] ToBytes(string data)
        {
            return ToBytes(data, Encoding.UTF8);
        }

        /// <summary>
        /// 字符串转换成字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="encoding">字符编码</param>
        public static byte[] ToBytes(string data, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return new byte[] { };
            }
            return encoding.GetBytes(data);
        }

        /// <summary>
        /// 流转换成字节流
        /// </summary>
        /// <param name="stream">流</param>
        public static byte[] ToBytes(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        /// <summary>
        /// 流转换成字节流
        /// </summary>
        /// <param name="stream">流</param>
        public static async Task<byte[]> ToBytesAsync(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            return buffer;
        }

        #endregion
    }
}
