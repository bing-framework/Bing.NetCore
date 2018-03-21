using System.Security.Cryptography;
using System.Text;
using Bing.Encryption.Core;

// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// HMAC_SHA384 哈希加密提供程序
    /// </summary>
    public sealed class HMACSHA384HashingProvider : HMACHashingBase
    {
        /// <summary>
        /// 初始化一个<see cref="HMACSHA384HashingProvider"/>类型的实例
        /// </summary>
        private HMACSHA384HashingProvider() { }

        /// <summary>
        /// 获取字符串的 HMAC_SHA384 哈希值，默认编码为<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Hex"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static string Signature(string value, string key, OutType outType = OutType.Hex,
            Encoding encoding = null) => Encrypt<HMACSHA384>(value, key, encoding, outType);

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="comparison">对比的值</param>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Hex"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static bool Verify(string comparison, string value, string key, OutType outType = OutType.Hex,
            Encoding encoding = null) => comparison == Signature(value, key, outType, encoding);
    }
}
