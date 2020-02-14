using System;
using System.Collections;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 基础类型扩展
    /// </summary>
    public static partial class BaseTypeExtensions
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

        /// <summary>
        /// 清空
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        public static void Clear(this Array array, int index, int length) => Array.Clear(array, index, length);

        /// <summary>
        /// 清空所有数据
        /// </summary>
        /// <param name="array">数组</param>
        public static void ClearAll(this Array array) => Array.Clear(array, 0, array.Length);

        /// <summary>
        /// 获取指定值的索引
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        public static int IndexOf(this Array array, object value) => Array.IndexOf(array, value);

        /// <summary>
        /// 获取指定值的索引
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        /// <param name="startIndex">起始索引</param>
        public static int IndexOf(this Array array, object value, int startIndex) =>
            Array.IndexOf(array, value, startIndex);

        /// <summary>
        /// 获取指定值的索引
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        /// <param name="startIndex">起始索引</param>
        /// <param name="count">计数</param>
        public static int IndexOf(this Array array, object value, int startIndex, int count) =>
            Array.IndexOf(array, value, startIndex, count);

        /// <summary>
        /// 获取指定值的最后索引
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        public static int LastIndexOf(this Array array, object value) => Array.LastIndexOf(array, value);

        /// <summary>
        /// 获取指定值的最后索引
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        /// <param name="startIndex">起始索引</param>
        public static int LastIndexOf(this Array array, object value, int startIndex) => Array.LastIndexOf(array, value, startIndex);

        /// <summary>
        /// 获取指定值的最后索引
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        /// <param name="startIndex">起始索引</param>
        /// <param name="count">计数</param>
        public static int LastIndexOf(this Array array, object value, int startIndex, int count) => Array.LastIndexOf(array, value, startIndex, count);

        /// <summary>
        /// 是否在数组索引范围内
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        public static bool WithInIndex(this Array array, int index) => array != null && index >= 0 && index < array.Length;

        /// <summary>
        /// 是否在数组索引范围内
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="dimension">数组维度</param>
        public static bool WithInIndex(this Array array, int index, int dimension)
        {
            if (dimension <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimension));
            return array != null && index >= array.GetLowerBound(dimension) && index <= array.GetUpperBound(dimension);
        }

        /// <summary>
        /// 反转
        /// </summary>
        /// <param name="array">数组</param>
        public static void Reverse(this Array array) => Array.Reverse(array);

        /// <summary>
        /// 反转
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        public static void Reverse(this Array array, int index, int length) => Array.Reverse(array, index, length);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        public static void Sort(this Array array) => Array.Sort(array);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        public static void Sort(this Array array, Array items) => Array.Sort(array, items);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        public static void Sort(this Array array, int index, int length) => Array.Sort(array, index, length);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        public static void Sort(this Array array, Array items, int index, int length) =>
            Array.Sort(array, items, index, length);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, IComparer comparer) => Array.Sort(array, comparer);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, Array items, IComparer comparer) =>
            Array.Sort(array, items, comparer);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, int index, int length, IComparer comparer) =>
            Array.Sort(array, index, length, comparer);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="items">其它项数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="comparer">比较器</param>
        public static void Sort(this Array array, Array items, int index, int length, IComparer comparer) =>
            Array.Sort(array, items, index, length, comparer);

        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        public static int BinarySearch(this Array array, object value) => Array.BinarySearch(array, value);

        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="value">值</param>
        public static int BinarySearch(this Array array, int index, int length, object value) =>
            Array.BinarySearch(array, index, length, value);

        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="value">值</param>
        /// <param name="comparer">比较器</param>
        public static int BinarySearch(this Array array, object value, IComparer comparer) =>
            Array.BinarySearch(array, value, comparer);

        /// <summary>
        /// 二进制查询
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        /// <param name="length">长度</param>
        /// <param name="value">值</param>
        /// <param name="comparer">比较器</param>
        public static int BinarySearch(this Array array, int index, int length, object value, IComparer comparer) =>
            Array.BinarySearch(array, index, length, value, comparer);

        /// <summary>
        /// 查找所有
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="me">数组</param>
        /// <param name="condition">条件</param>
        public static T[] FindAll<T>(this T[] me, Predicate<T> condition) => Array.FindAll(me, condition);
    }
}
