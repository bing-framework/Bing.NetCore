using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// GZip压缩操作
    /// </summary>
    public static class GZip
    {
        #region Compress(压缩)

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="content">内容</param>
        public static string Compress(string content) => Compress(content, Encoding.UTF8);

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="content">内容</param>
        public static async Task<string> CompressAsync(string content) => await CompressAsync(content, Encoding.UTF8);

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="content">内容</param>
        /// <param name="encoding">字符编码</param>
        public static string Compress(string content, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var buffer = encoding.GetBytes(content);
            return Convert.ToBase64String(Compress(buffer));
        }

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="content">内容</param>
        /// <param name="encoding">字符编码</param>
        public static async Task<string> CompressAsync(string content, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var buffer = encoding.GetBytes(content);
            return Convert.ToBase64String(await CompressAsync(buffer));
        }

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="buffer">字节流</param>
        public static byte[] Compress(byte[] buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            using (var ms = new MemoryStream())
            {
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zip.Write(buffer, 0, buffer.Length);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="buffer">字节流</param>
        public static async Task<byte[]> CompressAsync(byte[] buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            using (var ms = new MemoryStream())
            {
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    await zip.WriteAsync(buffer, 0, buffer.Length);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="stream">流</param>
        public static byte[] Compress(Stream stream)
        {
            if (stream == null || stream.Length == 0)
            {
                return null;
            }

            return Compress(StreamToBytes(stream));
        }

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="stream">流</param>
        public static async Task<byte[]> CompressAsync(Stream stream)
        {
            if (stream == null || stream.Length == 0)
            {
                return null;
            }

            return await CompressAsync(await StreamToBytesAsync(stream));
        }

        /// <summary>
        /// 流转换为字节流
        /// </summary>
        /// <param name="stream">流</param>
        private static byte[] StreamToBytes(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        /// <summary>
        /// 流转换为字节流
        /// </summary>
        /// <param name="stream">流</param>
        private static async Task<byte[]> StreamToBytesAsync(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            return buffer;
        }

        #endregion

        #region Decompress(解压缩)

        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="content">内容</param>
        public static string Decompress(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var buffer = Convert.FromBase64String(content);
            using (var ms = new MemoryStream(buffer))
            {
                using (var zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    using (var reader = new StreamReader(zip))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="content">内容</param>
        public static async Task<string> DecompressAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var buffer = Convert.FromBase64String(content);
            using (var ms = new MemoryStream(buffer))
            {
                using (var zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    using (var reader = new StreamReader(zip))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
            }
        }

        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="buffer">字节流</param>
        public static byte[] Decompress(byte[] buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            return Decompress(new MemoryStream(buffer));
        }

        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="stream">流</param>
        public static byte[] Decompress(Stream stream) => Decompress(stream, Encoding.UTF8);

        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="encoding">字符编码</param>
        public static byte[] Decompress(Stream stream, Encoding encoding)
        {
            if (stream == null || stream.Length == 0)
            {
                return null;
            }

            using (var zip = new GZipStream(stream, CompressionMode.Decompress))
            {
                using (var reader = new StreamReader(zip))
                {
                    return encoding.GetBytes(reader.ReadToEnd());
                }
            }
        }

        #endregion
    }
}
