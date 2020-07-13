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
        /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
        /// </summary>
        /// <param name="sourceArray">源数组</param>
        /// <param name="destinationArray">目标数组</param>
        /// <param name="length">长度</param>
        public static void Copy(this Array sourceArray, Array destinationArray, int length) =>
            Array.Copy(sourceArray, destinationArray, length);

        /// <summary>
        /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
        /// </summary>
        /// <param name="sourceArray">源数组</param>
        /// <param name="sourceIndex">源数组索引</param>
        /// <param name="destinationArray">目标数组</param>
        /// <param name="destinationIndex">目标数组索引</param>
        /// <param name="length">长度</param>
        public static void Copy(this Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex,
            int length) => Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);

        /// <summary>
        /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
        /// </summary>
        /// <param name="sourceArray">源数组</param>
        /// <param name="destinationArray">目标数组</param>
        /// <param name="length">长度</param>
        public static void Copy(this Array sourceArray, Array destinationArray, long length) =>
            Array.Copy(sourceArray, destinationArray, length);

        /// <summary>
        /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
        /// </summary>
        /// <param name="sourceArray">源数组</param>
        /// <param name="sourceIndex">源数组索引</param>
        /// <param name="destinationArray">目标数组</param>
        /// <param name="destinationIndex">目标数组索引</param>
        /// <param name="length">长度</param>
        public static void Copy(this Array sourceArray, long sourceIndex, Array destinationArray, long destinationIndex,
            long length) => Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);

        /// <summary>
        /// 复制。复制 Array 中的一系列元素（从指定的源索引开始），并将它们粘贴到另一 Array 中（从指定的目标索引开始）。 保证在复制未成功完成的情况下撤消所有更改。
        /// </summary>
        /// <param name="sourceArray">源数组</param>
        /// <param name="sourceIndex">源数组索引</param>
        /// <param name="destinationArray">目标数组</param>
        /// <param name="destinationIndex">目标数组索引</param>
        /// <param name="length">长度</param>
        public static void ConstrainedCopy(this Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length) => Array.ConstrainedCopy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);
    }
}
