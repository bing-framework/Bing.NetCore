using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Bing.Encryption.Core
{
    /// <summary>
    /// SHA哈希基类
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public abstract class SHAHashingBase
    {
        /// <summary>
        /// 加密
        /// </summary>
        /// <typeparam name="T">哈希算法类型</typeparam>
        /// <param name="value">待加密的值</param>
        /// <param name="encoding">编码</param>
        /// <param name="outType">输出类型</param>
        /// <returns></returns>
        protected static string Encrypt<T>(string value, Encoding encoding = null,OutType outType=OutType.Hex) where T : HashAlgorithm, new()
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (encoding == null)
            {
                encoding=Encoding.UTF8;
            }

            using (HashAlgorithm hash=new T())
            {
                var bytes = hash.ComputeHash(encoding.GetBytes(value));

                if (outType == OutType.Base64)
                {
                    return Convert.ToBase64String(bytes);
                }
                StringBuilder sb=new StringBuilder();
                foreach (var item in bytes)
                {
                    sb.AppendFormat("{0:x2}", item);
                }

                return sb.ToString();
            }
        }
    }
}
