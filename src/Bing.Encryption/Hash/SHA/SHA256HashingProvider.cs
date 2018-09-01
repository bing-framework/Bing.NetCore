using System.Security.Cryptography;
using System.Text;
using Bing.Encryption.Core;

// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// SHA256 哈希加密提供程序
    /// </summary>
    public sealed class SHA256HashingProvider:SHAHashingBase
    {
        /// <summary>
        /// 初始化一个<see cref="SHA256HashingProvider"/>类型的实例
        /// </summary>
        private SHA256HashingProvider() { }

        /// <summary>
        /// 获取字符串的 SHA256 哈希值，默认编码为<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Hex"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static string Signature(string value, OutType outType = OutType.Hex, Encoding encoding = null) =>
            Encrypt<SHA256CryptoServiceProvider>(value, encoding, outType);

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="comparison">对比的值</param>
        /// <param name="value">待加密的值</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Hex"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static bool Verify(string comparison, string value, OutType outType = OutType.Hex,
            Encoding encoding = null) => comparison == Signature(value, outType, encoding);
    }
}
