using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Bing.Encryption.Core
{
    /// <summary>
    /// 对称加密基类
    /// </summary>
    public abstract class SymmetricEncryptionBase
    {
        /// <summary>
        /// 获取真实 Key/IV 的值
        /// </summary>
        /// <returns></returns>
        protected static Func<string, Func<string, Func<Encoding, Func<int, byte[]>>>> ComputeRealValueFunc() =>
            originString => salt => encoding =>
                size =>
                {
                    if (string.IsNullOrWhiteSpace(originString))
                    {
                        return new byte[] {0};
                    }

                    if (encoding == null)
                    {
                        encoding = Encoding.UTF8;
                    }

                    var len = size / 8;

                    if (string.IsNullOrWhiteSpace(salt))
                    {
                        var retBytes = new byte[len];
                        Array.Copy(encoding.GetBytes(originString.PadRight(len)), retBytes, len);
                        return retBytes;
                    }

                    var saltBytes = encoding.GetBytes(salt);
                    var rfcOriginStringData = new Rfc2898DeriveBytes(encoding.GetBytes(originString), saltBytes, 1000);
                    return rfcOriginStringData.GetBytes(len);
                };

        /// <summary>
        /// 核心加密方法
        /// </summary>
        /// <typeparam name="TCryptoServiceProvider">对称加密算法类型</typeparam>
        /// <param name="sourceBytes">待加密的字节数组</param>
        /// <param name="keyBytes">密钥字节数组</param>
        /// <param name="ivBytes">偏移量字节数组</param>
        /// <returns></returns>
        protected static byte[] EncryptCore<TCryptoServiceProvider>(byte[] sourceBytes, byte[] keyBytes, byte[] ivBytes)
            where TCryptoServiceProvider : SymmetricAlgorithm, new()
        {
            using (var provider=new TCryptoServiceProvider())
            {
                provider.Key = keyBytes;
                provider.IV = ivBytes;
                using (MemoryStream ms=new MemoryStream())
                {
                    using (CryptoStream cs=new CryptoStream(ms,provider.CreateEncryptor(),CryptoStreamMode.Write))
                    {
                        cs.Write(sourceBytes,0,sourceBytes.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// 核心解密方法
        /// </summary>
        /// <typeparam name="TCryptoServiceProvider">对称加密算法类型</typeparam>
        /// <param name="encryptBytes">待解密的字节数组</param>
        /// <param name="keyBytes">密钥字节数组</param>
        /// <param name="ivBytes">偏移量字节数组</param>
        /// <returns></returns>
        protected static byte[] DecryptCore<TCryptoServiceProvider>(byte[] encryptBytes, byte[] keyBytes,
            byte[] ivBytes) where TCryptoServiceProvider : SymmetricAlgorithm, new()
        {
            using (var provider = new TCryptoServiceProvider())
            {
                provider.Key = keyBytes;
                provider.IV = ivBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, provider.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(encryptBytes, 0, encryptBytes.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
        }
    }
}
