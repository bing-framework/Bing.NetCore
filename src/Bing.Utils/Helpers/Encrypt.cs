using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers.Internal;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 加密操作
    /// 说明：
    /// 1、AES加密整理自支付宝SDK
    /// 2、RSA加密采用 https://github.com/stulzq/DotnetCore.RSA/blob/master/DotnetCore.RSA/RSAHelper.cs
    /// </summary>
    public static class Encrypt
    {
        #region Md5加密
        
        /// <summary>
        /// Md5加密，返回16位结果
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string Md5By16(string value)
        {
            return Md5By16(value, Encoding.UTF8);
        }

        /// <summary>
        /// Md5加密，返回16位结果
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string Md5By16(string value, Encoding encoding)
        {
            return Md5(value, encoding, 4, 8);
        }

        /// <summary>
        /// Md5加密，返回32位结果
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string Md5By32(string value)
        {
            return Md5By32(value, Encoding.UTF8);
        }

        /// <summary>
        /// Md5加密，返回32位结果
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string Md5By32(string value, Encoding encoding)
        {
            return Md5(value, encoding, null, null);
        }

        /// <summary>
        /// Md5加密
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="startIndex">开始索引</param>
        /// <param name="length">长度</param>
        /// <returns></returns>
        private static string Md5(string value, Encoding encoding, int? startIndex, int? length)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            var md5 = new MD5CryptoServiceProvider();
            string result;
            try
            {
                var hash = md5.ComputeHash(encoding.GetBytes(value));
                result = startIndex == null
                    ? BitConverter.ToString(hash)
                    : BitConverter.ToString(hash, startIndex.SafeValue(), length.SafeValue());
            }
            finally
            {
                md5.Clear();
            }
            return result.Replace("-", "");
        }

        #endregion

        #region DES加密

        /// <summary>
        /// DES密钥，24位字符串
        /// </summary>
        public static string DesKey = "#s^un2ye21fcv%|f0XpR,+vh";

        /// <summary>
        /// DES加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <returns></returns>
        public static string DesEncrypt(object value)
        {
            return DesEncrypt(value, DesKey);
        }

        /// <summary>
        /// DES加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥，24位</param>
        /// <returns></returns>
        public static string DesEncrypt(object value, string key)
        {
            return DesEncrypt(value, key, Encoding.UTF8);
        }

        /// <summary>
        /// DES加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥，24位</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string DesEncrypt(object value, string key, Encoding encoding)
        {
            var text = value.SafeString();
            if (ValidateDes(text, key) == false)
            {
                return string.Empty;
            }
            using (var transform=CreateDesProvider(key).CreateEncryptor())
            {
                return GetEncryptResult(text, encoding, transform);
            }
        }

        /// <summary>
        /// 验证Des加密参数
        /// </summary>
        /// <param name="text">待加密的文本</param>
        /// <param name="key">密钥，24位</param>
        /// <returns></returns>
        private static bool ValidateDes(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }
            return key.Length == 24;
        }

        /// <summary>
        /// 创建Des加密服务提供程序
        /// </summary>
        /// <param name="key">密钥，24位</param>
        /// <returns></returns>
        private static TripleDESCryptoServiceProvider CreateDesProvider(string key)
        {
            return new TripleDESCryptoServiceProvider()
            {
                Key = Encoding.ASCII.GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
        }

        /// <summary>
        /// 获取加密结果
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="transform">加密器</param>
        /// <returns></returns>
        private static string GetEncryptResult(string value, Encoding encoding, ICryptoTransform transform)
        {
            var bytes = encoding.GetBytes(value);
            var result = transform.TransformFinalBlock(bytes, 0, bytes.Length);
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// DES解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <returns></returns>
        public static string DesDecrypt(object value)
        {
            return DesDecrypt(value, DesKey);
        }

        /// <summary>
        /// DES解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <param name="key">密钥，24位</param>
        /// <returns></returns>
        public static string DesDecrypt(object value, string key)
        {
            return DesDecrypt(value, key, Encoding.UTF8);
        }

        /// <summary>
        /// DES解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <param name="key">密钥，24位</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string DesDecrypt(object value, string key, Encoding encoding)
        {
            var text = value.SafeString();
            if (!ValidateDes(text, key))
            {
                return string.Empty;
            }

            using (var transform=CreateDesProvider(key).CreateDecryptor())
            {
                return GetDecryptResult(text, encoding, transform);
            }
        }

        /// <summary>
        /// 获取解密结果
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="transform">加密器</param>
        /// <returns></returns>
        private static string GetDecryptResult(string value, Encoding encoding, ICryptoTransform transform)
        {
            var bytes = Convert.FromBase64String(value);
            var result = transform.TransformFinalBlock(bytes, 0, bytes.Length);
            return encoding.GetString(result);
        }

        #endregion

        #region AES加密

        /// <summary>
        /// 128位0向量
        /// </summary>
        private static byte[] _iv;

        /// <summary>
        /// 128位0向量
        /// </summary>
        private static byte[] Iv
        {
            get
            {
                if (_iv == null)
                {
                    var size = 16;
                    _iv=new byte[size];
                    for (int i = 0; i < size; i++)
                    {
                        _iv[i] = 0;
                    }
                }
                return _iv;
            }
        }

        /// <summary>
        /// AES密钥
        /// </summary>
        public static string AesKey = "QaP1AF8utIarcBqdhYTZpVGbiNQ9M6IL";

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <returns></returns>
        public static string AesEncrypt(string value)
        {
            return AesEncrypt(value, AesKey);
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static string AesEncrypt(string value, string key)
        {
            return AesEncrypt(value, key, Encoding.UTF8);
        }

        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string AesEncrypt(string value, string key, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }
            var rijndaelManaged = CreateRijndaelManaged(key);
            using (var transform = rijndaelManaged.CreateEncryptor(rijndaelManaged.Key, rijndaelManaged.IV))
            {
                return GetEncryptResult(value, encoding, transform);
            }
        }

        /// <summary>
        /// 创建RijndaelManaged
        /// </summary>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        private static RijndaelManaged CreateRijndaelManaged(string key)
        {
            return new RijndaelManaged()
            {
                Key = Convert.FromBase64String(key),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = Iv
            };
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <returns></returns>
        public static string AesDecrypt(string value)
        {
            return AesDecrypt(value, AesKey);
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static string AesDecrypt(string value, string key)
        {
            return AesDecrypt(value, key, Encoding.UTF8);
        }

        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="value">待解密的值</param>
        /// <param name="key">密钥</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string AesDecrypt(string value, string key, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var rijndaelManaged = CreateRijndaelManaged(key);
            using (var transform=rijndaelManaged.CreateDecryptor(rijndaelManaged.Key,rijndaelManaged.IV))
            {
                return GetDecryptResult(value, encoding, transform);
            }
        }

        #endregion

        #region RSA签名

        /// <summary>
        /// RSA签名，采用 SHA1 算法
        /// </summary>
        /// <param name="value">待签名的值</param>
        /// <param name="key">私钥</param>
        /// <returns></returns>
        public static string RsaSign(string value, string key)
        {
            return RsaSign(value, key, Encoding.UTF8);
        }
        /// <summary>
        /// RSA签名，采用 SHA1 算法
        /// </summary>
        /// <param name="value">待签名的值</param>
        /// <param name="key">私钥</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string RsaSign(string value, string key, Encoding encoding)
        {
            return RsaSign(value, key, encoding, RSAType.RSA);
        }
        /// <summary>
        /// RSA签名，采用 SHA256 算法
        /// </summary>
        /// <param name="value">待签名的值</param>
        /// <param name="key">私钥</param>
        /// <returns></returns>
        public static string Rsa2Sign(string value, string key)
        {
            return Rsa2Sign(value, key, Encoding.UTF8);
        }

        /// <summary>
        /// RSA签名，采用 SHA256 算法
        /// </summary>
        /// <param name="value">待签名的值</param>
        /// <param name="key">私钥</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string Rsa2Sign(string value, string key,Encoding encoding)
        {
            return RsaSign(value, key, encoding, RSAType.RSA2);
        }

        /// <summary>
        /// RSA签名
        /// </summary>
        /// <param name="value">待签名的值</param>
        /// <param name="key">私钥</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="type">RSA算法类型</param>
        /// <returns></returns>
        private static string RsaSign(string value, string key, Encoding encoding, RSAType type)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }
            var rsa = new RsaHelper(type, encoding, key);
            return rsa.Sign(value);
        }

        /// <summary>
        /// RSA验签，采用 SHA1 算法
        /// </summary>
        /// <param name="value">待验签的值</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="sign">签名</param>
        /// <returns></returns>
        public static bool RsaVerify(string value, string publicKey, string sign)
        {
            return RsaVerify(value, publicKey, sign, Encoding.UTF8);
        }

        /// <summary>
        /// RSA验签，采用 SHA1 算法
        /// </summary>
        /// <param name="value">待验签的值</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="sign">签名</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static bool RsaVerify(string value, string publicKey, string sign, Encoding encoding)
        {
            return RsaVerify(value, publicKey, sign, encoding, RSAType.RSA);
        }

        /// <summary>
        /// RSA验签，采用 SHA256 算法
        /// </summary>
        /// <param name="value">待验签的值</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="sign">签名</param>
        /// <returns></returns>
        public static bool Rsa2Verify(string value, string publicKey, string sign)
        {
            return Rsa2Verify(value, publicKey, sign, Encoding.UTF8);
        }

        /// <summary>
        /// RSA验签，采用 SHA256 算法
        /// </summary>
        /// <param name="value">待验签的值</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="sign">签名</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static bool Rsa2Verify(string value, string publicKey, string sign, Encoding encoding)
        {
            return RsaVerify(value, publicKey, sign, encoding, RSAType.RSA2);
        }

        /// <summary>
        /// RSA验签
        /// </summary>
        /// <param name="value">待验签的值</param>
        /// <param name="publicKey">公钥</param>
        /// <param name="sign">签名</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="type">RSA算法类型</param>
        /// <returns></returns>
        private static bool RsaVerify(string value, string publicKey, string sign, Encoding encoding, RSAType type)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            var rsa = new RsaHelper(type, encoding, publicKey: publicKey);
            return rsa.Verify(value, sign);
        }

        #endregion

        #region HmacSha256加密

        /// <summary>
        /// HMACSHA256加密
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static string HmacSha256(string value, string key)
        {
            return HmacSha256(value, key, Encoding.UTF8);
        }

        /// <summary>
        /// HMACSHA256加密
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="key">密钥</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string HmacSha256(string value, string key, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }
            var sha256 = new HMACSHA256(encoding.GetBytes(key));
            var hash = sha256.ComputeHash(encoding.GetBytes(value));
            return string.Join("", hash.ToList().Select(x => x.ToString("x2")).ToArray());
        }

        #endregion
    }
}
