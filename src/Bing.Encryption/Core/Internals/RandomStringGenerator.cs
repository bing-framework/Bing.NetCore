using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Bing.Encryption.Core.Internals
{
    /// <summary>
    /// 随机字符串生成器
    /// </summary>
    internal static class RandomStringGenerator
    {
        /// <summary>
        /// 字符串字典长度
        /// </summary>
        private static readonly int StringDictionaryLength;

        /// <summary>
        /// 字符串字典
        /// </summary>
        private const string STRING_DICTIONARY =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        /// <summary>
        /// 初始化一个<see cref="RandomStringGenerator"/>类型的静态实例
        /// </summary>
        static RandomStringGenerator()
        {
            StringDictionaryLength = STRING_DICTIONARY.Length;
        }

        /// <summary>
        /// 生成字符串
        /// </summary>
        /// <param name="bits">字符串长度，默认为8</param>
        /// <returns></returns>
        public static string Generate(int bits = 8)
        {
            var builder=new StringBuilder();

            var b = new byte[4];
            using (var provider=new RNGCryptoServiceProvider())
            {
                provider.GetBytes(b);
            }

            var random=new Random(BitConverter.ToInt32(b,0));

            for (int i = 0; i < bits; i++)
            {
                builder.Append(STRING_DICTIONARY.Substring(random.Next(0, StringDictionaryLength), 1));
            }

            return builder.ToString();
        }
    }
}
