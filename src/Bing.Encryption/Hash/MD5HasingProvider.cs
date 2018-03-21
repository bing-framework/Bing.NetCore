using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Bing.Encryption.Core.Internals.Extensions;
using Bing.Encryption.Hash;

// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// MD5 哈希加密提供程序
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static class MD5HasingProvider
    {
        /// <summary>
        /// 获取字符串的 MD5 哈希值，默认编码为<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="bitType">MD5加密类型，默认为<see cref="MD5BitType.L32"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static string Signature(string value, MD5BitType bitType = MD5BitType.L32, Encoding encoding = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (encoding == null)
            {
                encoding=Encoding.UTF8;
            }

            switch (bitType)
            {
                case MD5BitType.L16:
                    return Encrypt16Func()(value)(encoding);
                case MD5BitType.L32:
                    return Encrypt32Func()(value)(encoding);
                case MD5BitType.L64:
                    return Encrypt64Func()(value)(encoding);
                default:
                    throw new ArgumentOutOfRangeException(nameof(bitType), bitType, null);
            }
        }

        /// <summary>
        /// 预加密
        /// </summary>
        /// <returns></returns>
        private static Func<string, Func<Encoding, byte[]>> PreencryptFunc() => str => encoding =>
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(encoding.GetBytes(str));
            }
        };

        /// <summary>
        /// 16位加密
        /// </summary>
        /// <returns></returns>
        private static Func<string, Func<Encoding, string>> Encrypt16Func() => str =>
            encoding => BitConverter.ToString(PreencryptFunc()(str)(encoding), 4, 8).Replace("-", "");

        /// <summary>
        /// 32位加密
        /// </summary>
        /// <returns></returns>
        private static Func<string, Func<Encoding, string>> Encrypt32Func() => str => encoding =>
        {
            var bytes = PreencryptFunc()(str)(encoding);
            return bytes.ToHexString();
        };

        /// <summary>
        /// 64位加密
        /// </summary>
        /// <returns></returns>
        private static Func<string, Func<Encoding, string>> Encrypt64Func() => str =>
            encoding => Convert.ToBase64String(PreencryptFunc()(str)(encoding));

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="comparison">对比的值</param>
        /// <param name="value">待加密的值</param>
        /// <param name="bitType">MD5加密类型，默认为<see cref="MD5BitType.L32"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static bool Verify(string comparison, string value, MD5BitType bitType = MD5BitType.L32,
            Encoding encoding = null) => comparison == Signature(value, bitType, encoding);
    }
}
