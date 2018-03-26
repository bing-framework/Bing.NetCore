using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Bing.Encryption.Core;
using Bing.Encryption.Core.Internals.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// RSA 加密提供程序
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static class RSAEncryptionProvider
    {
        /// <summary>
        /// 创建 RSA 密钥
        /// </summary>
        /// <param name="size">密钥长度类型，默认为<see cref="RSAKeySizeType.L2048"/></param>
        /// <param name="keyType">密钥类型，默认为<see cref="RSAKeyType.Xml"/></param>
        /// <returns></returns>
        public static RSAKey CreateKey(RSAKeySizeType size = RSAKeySizeType.L2048, RSAKeyType keyType = RSAKeyType.Xml)
        {
            using (var rsa=new RSACryptoServiceProvider((int)size))
            {
                var publicKey = keyType == RSAKeyType.Json ? rsa.ToJsonString(false) : rsa.ToExtXmlString(false);

                var privateKey = keyType == RSAKeyType.Json ? rsa.ToJsonString(true) : rsa.ToExtXmlString(true);

                return new RSAKey()
                {
                    PublickKey = publicKey,
                    PrivateKey = privateKey,
                    Exponent = rsa.ExportParameters(false).Exponent.ToHexString(),
                    Modulus = rsa.ExportParameters(false).Modulus.ToHexString()
                };
            }
        }

        /// <summary>
        /// 从 xml 或 json 字符串中获取RSA实例
        /// </summary>
        /// <param name="rsaKey">rsa密钥</param>
        /// <param name="keyType">密钥类型，默认为<see cref="RSAKeyType.Xml"/></param>
        /// <returns></returns>
        public static RSA RSAFromString(string rsaKey, RSAKeyType keyType = RSAKeyType.Xml)
        {
            if (string.IsNullOrWhiteSpace(rsaKey))
            {
                throw new ArgumentNullException(nameof(keyType));
            }

            var rsa = RSA.Create();
            if (keyType == RSAKeyType.Xml)
            {
                rsa.FromExtXmlString(rsaKey);
            }
            else
            {
                rsa.FromJsonString(rsaKey);
            }

            return rsa;
        }

        /// <summary>
        /// 获取 RSA 私钥
        /// </summary>
        /// <param name="certFile">证书文件路径</param>
        /// <param name="password">密码</param>
        /// <returns></returns>
        public static string GetPrivateKey(string certFile, string password)
        {
            if (!File.Exists(certFile))
            {
                throw new FileNotFoundException(nameof(certFile));
            }

            var cert = new X509Certificate2(certFile, password, X509KeyStorageFlags.Exportable);
            return cert.PrivateKey.ToXmlString(true);
        }

        /// <summary>
        /// 获取 RSA 公钥
        /// </summary>
        /// <param name="certFile">证书文件路径</param>
        /// <returns></returns>
        public static string GetPublicKey(string certFile)
        {
            if (!File.Exists(certFile))
            {
                throw new FileNotFoundException(nameof(certFile));
            }

            var cert=new X509Certificate2(certFile);
            return cert.PublicKey.Key.ToXmlString(false);
        }

        public static string Encrypt(string value, string publicKey, Encoding encoding = null,
            OutType outType = OutType.Base64, RSAKeyType keyType = RSAKeyType.Xml)
        {
            if (encoding == null)
            {
                encoding=Encoding.UTF8;
            }

            return Encrypt(encoding.GetBytes(value), publicKey, outType, keyType);
        }

        public static string Encrypt(byte[] sourceBytes, string publicKey,OutType outType=OutType.Base64, RSAKeyType keyType = RSAKeyType.Xml)
        {
            using (var rsa=new RSACryptoServiceProvider())
            {
                if (keyType == RSAKeyType.Xml)
                {
                    rsa.FromExtXmlString(publicKey);
                }
                else
                {
                    rsa.FromJsonString(publicKey);
                }

                var result = rsa.Encrypt(sourceBytes, false);

                if (outType == OutType.Base64)
                {
                    return Convert.ToBase64String(result);
                }

                return result.ToHexString();
            }
        }

        public static string Decrypt(string value, string privateKey, Encoding encoding = null,
            OutType outType = OutType.Base64, RSAKeyType keyType = RSAKeyType.Xml)
        {
            return Decrypt(value.GetEncryptBytes(outType), privateKey, encoding, keyType);
        }

        public static string Decrypt(byte[] sourceBytes, string privateKey, Encoding encoding = null,RSAKeyType keyType = RSAKeyType.Xml)
        {
            if (encoding == null)
            {
                encoding=Encoding.UTF8;
            }

            using (var rsa=new RSACryptoServiceProvider())
            {
                if (keyType == RSAKeyType.Xml)
                {
                    rsa.FromExtXmlString(privateKey);
                }
                else
                {
                    rsa.FromJsonString(privateKey);
                }

                return encoding.GetString(rsa.Decrypt(sourceBytes, false));
            }
        }

        public static bool GetHash(string value, ref byte[] hash, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            hash = HashStringFunc()(value)(encoding);
            return true;
        }

        public static bool GetHash(string value, ref string hash, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            hash = Convert.ToBase64String(HashStringFunc()(value)(encoding));
            return true;
        }

        private static Func<string, Func<Encoding, byte[]>> HashStringFunc() => value =>
            encoding => HashAlgorithm.Create("MD5").ComputeHash(encoding.GetBytes(value));

        public static bool GetHash(FileStream fs, ref byte[] hash)
        {
            hash = HashFileFunc()(fs);
            return true;
        }

        public static bool GetHash(FileStream fs, ref string hash)
        {
            hash = Convert.ToBase64String(HashFileFunc()(fs));
            return true;
        }

        private static Func<FileStream, byte[]> HashFileFunc() => fs =>
        {
            var ret = HashAlgorithm.Create("MD5").ComputeHash(fs);
            fs.Close();
            return ret;
        };
    }
}
