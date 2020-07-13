using System;
using System.Text;

namespace Bing.Conversions
{
    /// <summary>
    /// Base64 转换器
    /// </summary>
    public static class Base64Converter
    {
        /// <summary>
        /// Base64 种子
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private const string BASE64 = "===========================================+=+=/0123456789=======ABCDEFGHIJKLMNOPQRSTUVWXYZ====/=abcdefghijklmnopqrstuvwxyz=====";

        /// <summary>
        /// 转换为Base64字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string ToBase64String(byte[] bytes) => Convert.ToBase64String(bytes);

        /// <summary>
        /// 转换为Base64字符串
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="encoding">字符编码</param>
        public static string ToBase64String(string str, Encoding encoding = null) => ToBase64String(encoding.Fixed().GetBytes(str));

        /// <summary>
        /// 将Base64字符串转换为字符串
        /// </summary>
        /// <param name="base64String">Base64字符串</param>
        /// <param name="encoding">字符编码</param>
        public static string FromBase64String(string base64String, Encoding encoding = null) => encoding.Fixed().GetString(FromBase64StringToBytes(base64String));

        /// <summary>
        /// 将Base64字符串转换为字节数组
        /// </summary>
        /// <param name="base64String">Base64字符串</param>
        public static byte[] FromBase64StringToBytes(string base64String) => Convert.FromBase64String(base64String);

        /// <summary>
        /// 转换为Base64Url字符串
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="encoding">字符编码</param>
        public static string ToBase64UrlString(string str, Encoding encoding = null) => ToBase64UrlString(encoding.Fixed().GetBytes(str));

        /// <summary>
        /// 转换为Base64Url字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string ToBase64UrlString(byte[] bytes) => new StringBuilder(Convert.ToBase64String(bytes).TrimEnd('=')).Replace('+', '-').Replace('/', '_').ToString();

        /// <summary>
        /// 将Base64Url字符串转换为字符串
        /// </summary>
        /// <param name="base64UrlString">Base64Url字符串</param>
        /// <param name="encoding">字符编码</param>
        public static string FromBase64UrlString(string base64UrlString, Encoding encoding = null) => encoding.Fixed().GetString(FromBase64UrlStringToBytes(base64UrlString));

        /// <summary>
        /// 将Base64Url字符串转换为字节数组
        /// </summary>
        /// <param name="base64UrlString">Base64Url字符串</param>
        public static byte[] FromBase64UrlStringToBytes(string base64UrlString)
        {
            var sb = new StringBuilder();
            foreach (var c in base64UrlString)
            {
                if (c >= 128)
                    continue;
                var k = BASE64[c];
                if (k == '=')
                    continue;
                sb.Append(k);
            }

            var len = sb.Length;
            var padChars = (len % 4) == 0 ? 0 : (4 - (len % 4));
            if (padChars > 0)
                sb.Append(string.Empty.PadRight(padChars, '='));
            return Convert.FromBase64String(sb.ToString());
        }

        /// <summary>
        /// 补全。如果编码为空，则默认返回<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="encoding">编码</param>
        private static Encoding Fixed(this Encoding encoding) => encoding ?? Encoding.UTF8;
    }
}
