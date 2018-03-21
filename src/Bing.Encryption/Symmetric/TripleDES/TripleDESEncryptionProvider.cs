using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Bing.Encryption.Core;
using Bing.Encryption.Core.Internals.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// TripleDES 加密提供程序
    /// </summary>
    public sealed class TripleDESEncryptionProvider:SymmetricEncryptionBase
    {
        /// <summary>
        /// 初始化一个<see cref="TripleDESEncryptionProvider"/>类型的实例
        /// </summary>
        private TripleDESEncryptionProvider() { }

        /// <summary>
        /// 创建 AES 密钥
        /// </summary>
        /// <param name="size">密钥长度类型，默认为<see cref="TripleDESKeySizeType.L192"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static TripleDESKey CreateKey(TripleDESKeySizeType size = TripleDESKeySizeType.L192, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            using (var provider = new TripleDESCryptoServiceProvider())
            {
                return new TripleDESKey()
                {
                    Key = encoding.GetString(provider.Key),
                    IV = encoding.GetString(provider.IV),
                    Size = size
                };
            }
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥</param>
        /// <param name="iv">加密偏移量</param>
        /// <param name="salt">加盐</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Base64"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <param name="keySize">密钥长度类型，默认为<see cref="TripleDESKeySizeType.L192"/></param>
        /// <returns></returns>
        public static string Encrypt(string value, string key, string iv = null, string salt = null,
            OutType outType = OutType.Base64,
            Encoding encoding = null, TripleDESKeySizeType keySize = TripleDESKeySizeType.L192)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            var result = EncryptCore<TripleDESCryptoServiceProvider>(encoding.GetBytes(value),
                ComputeRealValueFunc()(key)(salt)(encoding)((int)keySize),
                ComputeRealValueFunc()(iv)(salt)(encoding)(64));

            if (outType == OutType.Base64)
            {
                return Convert.ToBase64String(result);
            }

            return result.ToHexString();
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">TripleDES 密钥对象</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Base64"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static string Encrypt(string value, TripleDESKey key, OutType outType = OutType.Base64,
            Encoding encoding = null)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return Encrypt(value, key.Key, key.IV, outType: outType, encoding: encoding, keySize: key.Size);
        }

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <param name="key">密钥</param>
        /// <param name="iv">加密偏移量</param>
        /// <param name="salt">加盐</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Base64"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <param name="keySize">密钥长度类型，默认为<see cref="TripleDESKeySizeType.L192"/></param>
        /// <returns></returns>
        public static string Decrypt(string value, string key, string iv = null, string salt = null,
            OutType outType = OutType.Base64,
            Encoding encoding = null, TripleDESKeySizeType keySize = TripleDESKeySizeType.L192)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            var result = DecryptCore<AesCryptoServiceProvider>(value.GetEncryptBytes(outType),
                ComputeRealValueFunc()(key)(salt)(encoding)((int)keySize),
                ComputeRealValueFunc()(iv)(salt)(encoding)(64));

            if (outType == OutType.Base64)
            {
                return Convert.ToBase64String(result);
            }

            return result.ToHexString();
        }

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">TripleDES 密钥对象</param>
        /// <param name="outType">输出类型，默认为<see cref="OutType.Base64"/></param>
        /// <param name="encoding">编码类型，默认为<see cref="Encoding.UTF8"/></param>
        /// <returns></returns>
        public static string Decrypt(string value, TripleDESKey key, OutType outType = OutType.Base64,
            Encoding encoding = null)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return Decrypt(value, key.Key, key.IV, outType: outType, encoding: encoding, keySize: key.Size);
        }
    }
}
