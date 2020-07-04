using System.Text;
using Bing.Conversions;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 将Base64字符串转换为字节数组
        /// </summary>
        /// <param name="base64String">Base64字符串</param>
        public static byte[] FromBase64StringToBytes(this string base64String) =>
            Base64Converter.FromBase64StringToBytes(base64String);

        /// <summary>
        /// 将Base64字符串转换为字符串
        /// </summary>
        /// <param name="base64String">Base64字符串</param>
        /// <param name="encoding">字符编码</param>
        public static string FromBase64String(this string base64String, Encoding encoding = null) =>
            Base64Converter.FromBase64String(base64String, encoding);
    }
}
