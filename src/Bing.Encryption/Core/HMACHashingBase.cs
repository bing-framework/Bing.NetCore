using System;
using System.Security.Cryptography;
using System.Text;
using Bing.Encryption.Core.Internals.Extensions;

namespace Bing.Encryption.Core
{
    /// <summary>
    /// HMAC哈希加密基类
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public abstract class HMACHashingBase
    {
        /// <summary>
        /// 加密
        /// </summary>
        /// <typeparam name="T">密钥哈希算法类型</typeparam>
        /// <param name="value">待加密的值</param>
        /// <param name="key">密钥</param>
        /// <param name="encoding">编码</param>
        /// <param name="outType">输出类型</param>
        /// <returns></returns>
        protected static string Encrypt<T>(string value,string key, Encoding encoding = null, OutType outType = OutType.Hex)
            where T : KeyedHashAlgorithm, new()
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            using (KeyedHashAlgorithm hash=new T())
            {
                hash.Key = encoding.GetBytes(key);
                var result=hash.ComputeHash(encoding.GetBytes(value));

                if (outType == OutType.Base64)
                {
                    return Convert.ToBase64String(result);
                }

                return result.ToHexString();
            }
        }
    }
}
