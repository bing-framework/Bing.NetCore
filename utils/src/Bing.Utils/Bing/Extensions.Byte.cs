using System;
using System.IO;
using System.Text;
using Bing.Conversions;
using Bing.Text;

namespace Bing
{
    /// <summary>
    /// 字节(<see cref="byte"/>) 扩展
    /// </summary>
    public static class ByteExtensions
    {
        #region Encoding

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="encoding">字符编码。默认为：<see cref="Encoding.UTF8"/></param>
        public static string GetString(this byte[] bytes, Encoding encoding = null) => bytes is null
            ? throw new ArgumentNullException(nameof(bytes))
            : (encoding ?? Encoding.UTF8).GetString(bytes);

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string GetStringByUtf8(this byte[] bytes) => bytes.GetString(Encoding.UTF8);

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string GetStringByUtf7(this byte[] bytes) => bytes.GetString(Encoding.UTF7);

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string GetStringByUtf32(this byte[] bytes) => bytes.GetString(Encoding.UTF32);

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        // ReSharper disable once InconsistentNaming
        public static string GetStringByASCII(this byte[] bytes) => bytes.GetString(Encoding.ASCII);

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string GetStringByBigEndianUnicode(this byte[] bytes) => bytes.GetString(Encoding.BigEndianUnicode);

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string GetStringByDefault(this byte[] bytes) => bytes.GetString(Encoding.Default);

        /// <summary>
        /// 转换字节数组为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public static string GetStringByUnicode(this byte[] bytes) => bytes.GetString(Encoding.Unicode);

        /// <summary>
        /// 转换为Base64字节数组
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="encoding">字符编码</param>
        public static byte[] ToBase64Bytes(this byte[] bytes, Encoding encoding = null) => bytes.ToBase64String().ToBytes(encoding);

        /// <summary>
        /// 转换为普通字节数组
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="encoding">字符编码</param>
        public static byte[] DecodeBase64ToBytes(this byte[] bytes, Encoding encoding = null) => bytes.GetString(encoding).FromBase64StringToBytes();

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="encoding">字符编码</param>
        public static string DecodeBase64ToString(this byte[] bytes, Encoding encoding = null) => bytes.GetString(encoding).FromBase64String();

        #endregion

        #region Min & Max

        /// <summary>
        /// 获取最大值
        /// </summary>
        /// <param name="val1">值1</param>
        /// <param name="val2">值2</param>
        public static byte Max(this byte val1, byte val2) => Math.Max(val1, val2);

        /// <summary>
        /// 获取最小值
        /// </summary>
        /// <param name="val1">值1</param>
        /// <param name="val2">值2</param>
        public static byte Min(this byte val1, byte val2) => Math.Min(val1, val2);

        #endregion

        #region Resize

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

        #endregion

        #region ToBase64

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
        public static string ToBase64String(this byte[] inArray) => Base64Converter.ToBase64String(inArray);

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

        #endregion

        #region ToMemoryStream

        /// <summary>
        /// 转换为内存流
        /// </summary>
        /// <param name="this">byte[]</param>
        public static MemoryStream ToMemoryStream(this byte[] @this) => new MemoryStream(@this);

        #endregion
    }
}
