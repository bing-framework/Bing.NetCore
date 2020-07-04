using System;
using System.Text;
using Bing.Extensions;
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



        #endregion
    }
}
