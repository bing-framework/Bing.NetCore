using System;

// ReSharper disable once CheckNamespace
namespace Bing.Collections
{
    /// <summary>
    /// 数组(<see cref="Array"/>) 扩展
    /// </summary>
    public static partial class ArrayExtensions
    {
        /// <summary>
        /// 块复制
        /// </summary>
        /// <param name="src">源数组</param>
        /// <param name="srcOffset">源数组偏移量</param>
        /// <param name="dst">目标数组</param>
        /// <param name="dstOffset">目标数组偏移量</param>
        /// <param name="count">计数</param>
        public static void BlockCopy(this Array src, int srcOffset, Array dst, int dstOffset, int count) =>
            Buffer.BlockCopy(src, srcOffset, dst, dstOffset, count);

        /// <summary>
        /// 获取字节长度
        /// </summary>
        /// <param name="array">数组</param>
        public static int ByteLength(this Array array) => Buffer.ByteLength(array);

        /// <summary>
        /// 获取字节
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        public static byte GetByte(this Array array, int index) => Buffer.GetByte(array, index);

        /// <summary>
        /// 设置字节
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="value">值</param>
        public static void SetByte(this Array array, int index, byte value) => Buffer.SetByte(array, index, value);
    }
}
