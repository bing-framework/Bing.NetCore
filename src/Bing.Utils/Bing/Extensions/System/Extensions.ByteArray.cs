using System;
using System.IO;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    public static partial class BaseTypeExtensions
    {
        /// <summary>
        /// 重置数组大小
        /// </summary>
        /// <param name="this">byte[]</param>
        /// <param name="newSize">新数组的大小</param>
        public static byte[] Resize(this byte[] @this, int newSize)
        {
            Array.Resize(ref @this, newSize);
            return @this;
        }

        /// <summary>
        /// 转换为Base64字符数组
        /// </summary>
        /// <param name="inArray">byte[]</param>
        /// <param name="offsetIn">输入偏移量</param>
        /// <param name="length">长度</param>
        /// <param name="outArray">输出字符数组</param>
        /// <param name="offsetOut">输出偏移量</param>
        public static int ToBase64CharArray(this byte[] inArray, int offsetIn, int length, char[] outArray,
            int offsetOut) => Convert.ToBase64CharArray(inArray, offsetIn, length, outArray, offsetOut);

        /// <summary>
        /// 转换为Base64字符数组
        /// </summary>
        /// <param name="inArray">byte[]</param>
        /// <param name="offsetIn">输入偏移量</param>
        /// <param name="length">长度</param>
        /// <param name="outArray">输出字符数组</param>
        /// <param name="offsetOut">输出偏移量</param>
        /// <param name="options">Base64格式化配置</param>
        public static int ToBase64CharArray(this byte[] inArray, int offsetIn, int length, char[] outArray,
            int offsetOut, Base64FormattingOptions options) =>
            Convert.ToBase64CharArray(inArray, offsetIn, length, outArray, offsetOut, options);

        /// <summary>
        /// 转换为Base64字符串
        /// </summary>
        /// <param name="inArray">byte[]</param>
        /// <param name="options">Base64格式化配置</param>
        public static string ToBase64String(this byte[] inArray, Base64FormattingOptions options) => Convert.ToBase64String(inArray, options);

        /// <summary>
        /// 转换为Base64字符串
        /// </summary>
        /// <param name="inArray">byte[]</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">长度</param>
        public static string ToBase64String(this byte[] inArray, int offset, int length) => Convert.ToBase64String(inArray, offset, length);

        /// <summary>
        /// 转换为Base64字符串
        /// </summary>
        /// <param name="inArray">byte[]</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">长度</param>
        /// <param name="options">Base64格式化配置</param>
        public static string ToBase64String(this byte[] inArray, int offset, int length,
            Base64FormattingOptions options) => Convert.ToBase64String(inArray, offset, length, options);

        /// <summary>
        /// 转换为内存流
        /// </summary>
        /// <param name="this">byte[]</param>
        public static MemoryStream ToMemoryStream(this byte[] @this) => new MemoryStream(@this);
    }
}
