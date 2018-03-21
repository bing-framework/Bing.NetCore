using System;
using System.Collections.Generic;
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

        //protected static byte[] EncryptCore<TCryptoServiceProvider>(byte[] sourceBytes, byte[] keyBytes, byte[] ivBytes)
        //    where TCryptoServiceProvider : SymmetricAlgorithm, new()
        //{

        //}
    }
}
