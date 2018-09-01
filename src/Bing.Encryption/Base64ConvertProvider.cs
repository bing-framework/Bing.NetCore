using System;
using System.Text;

namespace Bing.Encryption
{
    /// <summary>
    /// Base64 转换提供程序
    /// </summary>
    public static class Base64ConvertProvider
    {
        /// <summary>
        /// 加密，返回Base64字符串
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static string Encode(string value, Encoding encoding = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (encoding == null)
            {
                encoding=Encoding.UTF8;
            }

            return Convert.ToBase64String(encoding.GetBytes(value));
        }

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static string Decode(string value, Encoding encoding = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            return encoding.GetString(Convert.FromBase64String(value));
        }
    }
}
