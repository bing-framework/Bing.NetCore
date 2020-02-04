using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 数组(<see cref="Array"/>) 扩展
    /// </summary>
    public static partial class ArrayExtensions
    {
        #region WithInIndex(判断索引是否在数组中)

        /// <summary>
        /// 判断索引是否在数组中
        /// </summary>
        /// <param name="source">数组</param>
        /// <param name="index">索引</param>
        public static bool WithInIndex(this Array source, int index) => source != null && index >= 0 && index < source.Length;

        /// <summary>
        /// 判断索引是否在数组中
        /// </summary>
        /// <param name="source">数组</param>
        /// <param name="index">索引</param>
        /// <param name="dimension">数组维度</param>
        public static bool WithInIndex(this Array source, int index, int dimension) =>
            source != null && index >= source.GetLowerBound(dimension) &&
            index <= source.GetUpperBound(dimension);

        #endregion

        #region CombineArray(合并数组)

        /// <summary>
        /// 合并数组，合并两个数组到一个新的数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="combineWith">源数组</param>
        /// <param name="arrayToCombine">目标数组</param>
        /// <example>
        /// 	<code>
        /// 		int[] arrayOne = new[] { 1, 2, 3, 4 };
        /// 		int[] arrayTwo = new[] { 5, 6, 7, 8 };
        /// 		Array combinedArray = arrayOne.CombineArray&lt;int&gt;(arrayTwo);
        /// 	</code>
        /// </example>
        public static T[] CombineArray<T>(this T[] combineWith, T[] arrayToCombine)
        {
            if (combineWith != default(T[]) && arrayToCombine != default(T[]))
            {
                int initialSize = combineWith.Length;
                Array.Resize(ref combineWith, initialSize + arrayToCombine.Length);
                Array.Copy(arrayToCombine, arrayToCombine.GetLowerBound(0), combineWith, initialSize,
                    arrayToCombine.Length);
            }
            return combineWith;
        }

        #endregion

        #region ClearAll(清空数组内容)

        /// <summary>
        /// 清空数组内容
        /// </summary>
        /// <param name="source">源数组</param>
        public static Array ClearAll(this Array source)
        {
            if (source != null)
                Array.Clear(source, 0, source.Length);
            return source;
        }

        /// <summary>
        /// 清空数组内容
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="source">源数组</param>
        public static T[] ClearAll<T>(this T[] source)
        {
            if (source != null)
            {
                for (var i = source.GetLowerBound(0); i <= source.GetUpperBound(0); ++i)
                    source[i] = default(T);
            }
            return source;
        }

        #endregion

        #region ClearAt(清除数组中指定索引的内容)

        /// <summary>
        /// 清除数组中指定索引的内容
        /// </summary>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        public static Array ClearAt(this Array array, int index)
        {
            if (array != null)
            {
                var arrayIndex = index.GetArrayIndex();
                if (arrayIndex.IsIndexInArray(array))
                    Array.Clear(array, arrayIndex, 1);
            }

            return array;
        }

        /// <summary>
        /// 清除数组中指定索引的内容
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="index">索引</param>
        public static T[] ClearAt<T>(this T[] array, int index)
        {
            if (array != null)
            {
                var arrayIndex = index.GetArrayIndex();
                if (arrayIndex.IsIndexInArray(array))
                    array[arrayIndex] = default;
            }

            return array;
        }

        #endregion

        #region BlockCopy(复制数据块)

        /// <summary>
        /// 复制数据块，复制数组内容到新数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="index">索引</param>
        /// <param name="length">复制长度</param>
        public static T[] BlockCopy<T>(this T[] source, int index, int length) => BlockCopy(source, index, length, false);

        /// <summary>
        /// 复制数据块，复制数组内容到新数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="index">索引</param>
        /// <param name="length">复制长度</param>
        /// <param name="padToLength">是否填充指定长度</param>
        public static T[] BlockCopy<T>(this T[] source, int index, int length, bool padToLength)
        {
            if (source == null)
                throw new NullReferenceException(nameof(source));
            var n = length;
            T[] b = null;
            if (source.Length < index + length)
            {
                n = source.Length - index;// n=source数组剩余长度
                if (padToLength)
                    b = new T[length];
            }

            if (b == null)
                b = new T[n];
            Array.Copy(source, index, b, 0, n);// 从source数组指定索引开始复制数据到b数组当中，直至到达指定长度结束复制
            return b;
        }

        /// <summary>
        /// 复制数据块，复制数组内容到新数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="length">复制长度</param>
        /// <param name="padToLength">是否填充指定长度</param>
        public static IEnumerable<T[]> BlockCopy<T>(this T[] source, int length, bool padToLength)
        {
            for (var i = 0; i < source.Length; i += length)
                yield return source.BlockCopy(i, length, padToLength);
        }

        #endregion
    }
}
