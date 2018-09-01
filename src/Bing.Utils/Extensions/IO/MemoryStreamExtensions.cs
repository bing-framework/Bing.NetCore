using System.IO;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 内存流(<see cref="MemoryStream"/>) 扩展
    /// </summary>
    public static class MemoryStreamExtensions
    {
        /// <summary>
        /// 转换成字符串输出
        /// </summary>
        /// <param name="ms">内存流</param>
        /// <param name="encoding">字符编码，默认值：UTF-8</param>
        /// <returns></returns>
        public static string AsString(this MemoryStream ms, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            return encoding.GetString(ms.ToArray());
        }

        /// <summary>
        /// 写入字符串到内存流中
        /// </summary>
        /// <param name="ms">内存流</param>
        /// <param name="input">输入值</param>
        /// <param name="encoding">字符编码，默认值：UTF-8</param>
        public static void FromString(this MemoryStream ms, string input, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            byte[] buffer = encoding.GetBytes(input);
            ms.Write(buffer, 0, buffer.Length);
        }
    }
}
