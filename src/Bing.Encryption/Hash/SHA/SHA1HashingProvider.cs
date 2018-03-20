using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Bing.Encryption.Core;

// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// SHA1 哈希提供程序
    /// </summary>
    public sealed class SHA1HashingProvider:SHAHashingBase
    {
        /// <summary>
        /// 初始化一个<see cref="SHA1HashingProvider"/>类型的实例
        /// </summary>
        private SHA1HashingProvider() { }

        /// <summary>
        /// 获取字符串的 SHA1 哈希值，默认编码为<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="outType">输出类型</param>
        /// <param name="encoding">编码类型</param>
        /// <returns></returns>
        public static string Signature(string value, OutType outType = OutType.Hex, Encoding encoding = null) =>
            Encrypt<SHA1CryptoServiceProvider>(value, encoding, outType);
    }
}
