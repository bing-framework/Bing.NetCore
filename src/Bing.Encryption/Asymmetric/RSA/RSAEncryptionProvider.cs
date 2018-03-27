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
        public static RSA RsaFromString(string rsaKey, RSAKeyType keyType = RSAKeyType.Xml)
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

        /// <summary>
        /// 使用指定公钥加密字符串
        /// </summary>
        /// <param name="value">要加密的明文字符串</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="encoding">编码类型</param>
        /// <param name="outType">输出类型</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static string Encrypt(string value, string publicKey, Encoding encoding = null,
            OutType outType = OutType.Base64, RSAKeyType keyType = RSAKeyType.Xml)
        {
            if (encoding == null)
            {
                encoding=Encoding.UTF8;
            }

            var result = Encrypt(encoding.GetBytes(value), publicKey, keyType);

            if (outType == OutType.Base64)
            {
                return Convert.ToBase64String(result);
            }

            return result.ToHexString();
        }

        /// <summary>
        /// 使用指定公钥加密字节数组
        /// </summary>
        /// <param name="sourceBytes">要加密的明文字节数组</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] sourceBytes, string publicKey, RSAKeyType keyType = RSAKeyType.Xml)
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

                return rsa.Encrypt(sourceBytes, RSAEncryptionPadding.Pkcs1);
            }
        }

        /// <summary>
        /// 使用指定私钥解密字符串
        /// </summary>
        /// <param name="value">要解密的密文字符串</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="encoding">编码类型</param>
        /// <param name="outType">输出类型</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static string Decrypt(string value, string privateKey, Encoding encoding = null,
            OutType outType = OutType.Base64, RSAKeyType keyType = RSAKeyType.Xml)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            var result= Decrypt(value.GetEncryptBytes(outType), privateKey, keyType);
            return encoding.GetString(result);
        }

        /// <summary>
        /// 使用指定私钥解密字节数组
        /// </summary>
        /// <param name="sourceBytes">要解密的密文字节数组</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] sourceBytes, string privateKey,RSAKeyType keyType = RSAKeyType.Xml)
        {            
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

                return rsa.Decrypt(sourceBytes, RSAEncryptionPadding.Pkcs1);
            }
        }

        /// <summary>
        /// 使用指定密钥对明文进行签名，返回明文签名的字符串
        /// </summary>
        /// <param name="source">要签名的明文字符串</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="encoding">编码类型</param>
        /// <param name="outType">输出类型</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static string SignData(string source, string privateKey,Encoding encoding=null, OutType outType = OutType.Base64,
            RSAKeyType keyType = RSAKeyType.Xml)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            var result = SignData(encoding.GetBytes(source), privateKey, keyType);
            if (outType == OutType.Base64)
            {
                return Convert.ToBase64String(result);
            }

            return result.ToHexString();
        }

        /// <summary>
        /// 使用指定私钥对明文进行签名，返回明文签名的字节数组
        /// </summary>
        /// <param name="source">要签名的明文字节数组</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static byte[] SignData(byte[] source, string privateKey, RSAKeyType keyType = RSAKeyType.Xml)
        {
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

                return rsa.SignData(source, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            }
        }

        /// <summary>
        /// 使用指定公钥验证解密得到的明文是否符合签名
        /// </summary>
        /// <param name="source">解密得到的明文</param>
        /// <param name="signData">明文签名字符串</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="encoding">编码类型</param>
        /// <param name="outType">输出类型</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static bool VerifyData(string source, string signData, string publicKey, Encoding encoding = null,
            OutType outType = OutType.Base64, RSAKeyType keyType = RSAKeyType.Xml)
        {
            if (encoding == null)
            {
                encoding=Encoding.UTF8;
            }
            byte[] sourceBytes = encoding.GetBytes(source);
            byte[] signBytes = signData.GetEncryptBytes(outType);

            return VerifyData(sourceBytes, signBytes, publicKey, keyType);
        }

        /// <summary>
        /// 使用指定公钥验证解密得到的明文是否符合签名
        /// </summary>
        /// <param name="source">解密得到的明文字节数组</param>
        /// <param name="signData">明文签名字节数组</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns></returns>
        public static bool VerifyData(byte[] source, byte[] signData, string publicKey, RSAKeyType keyType = RSAKeyType.Xml)
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
                return rsa.VerifyData(source, signData, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            }
        }
    }
}
