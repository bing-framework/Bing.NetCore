using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 列表(<see cref="IList{T}"/>) 扩展
    /// </summary>
    public static class ListExtensions
    {
        #region InsertIfNotExists(插入项。如果不存在，则插入)

        /// <summary>
        /// 插入项。如果不存在，则插入
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="index">索引</param>
        /// <param name="item">项</param>
        public static bool InsertIfNotExists<T>(this IList<T> list, int index, T item)
        {
            if (list.Contains(item) == false)
            {
                list.Insert(index, item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 批量插入项。如果不存在，则插入
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="startIndex">开始位置索引</param>
        /// <param name="items">列表项</param>
        public static int InsertIfNotExists<T>(this IList<T> list, int startIndex, IEnumerable<T> items)
        {
            var index = startIndex + items.Reverse().Count(item => list.InsertIfNotExists(startIndex, item));
            return (index - startIndex);
        }

        #endregion

        #region IndexOf(获取第一匹配项的索引)

        /// <summary>
        /// 获取第一匹配项的索引
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="comparison">条件</param>
        public static int IndexOf<T>(this IList<T> list, Func<T, bool> comparison)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (comparison(list[i]))
                    return i;
            }
            return -1;
        }

        #endregion

        #region Join(将列表连接为字符串)

        /// <summary>
        /// 将列表连接为字符串，根据指定的字符进行分割
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="joinChar">分割符</param>
        public static string Join<T>(this IList<T> list, char joinChar) => list.Join(joinChar.ToString());

        /// <summary>
        /// 将列表连接为字符串，根据指定的字符串进行连接
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="joinString">分割字符串</param>
        public static string Join<T>(this IList<T> list, string joinString)
        {
            if (list == null || !list.Any())
                return string.Empty;
            var sb=new StringBuilder();
            var listCount = list.Count;
            var listCountMinusOne = listCount - 1;

            if (listCount > 1)
            {
                for (var i = 0; i < listCount; i++)
                {
                    if (i != listCountMinusOne)
                    {
                        sb.Append(list[i]);
                        sb.Append(joinString);
                    }
                    else
                    {
                        sb.Append(list[i]);
                    }
                }
            }
            else
            {
                sb.Append(list[0]);
            }

            return sb.ToString();
        }

        #endregion

        #region EqaulsAll(是否完全相等)

        /// <summary>
        /// 是否完全相等
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">待比较列表</param>
        /// <param name="other">待比较列表</param>
        public static bool EqualsAll<T>(this IList<T> list, IList<T> other)
        {
            if (list == null || other == null)
                return list == null && other == null;
            if (list.Count != other.Count)
                return false;
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < list.Count; i++)
            {
                if (!comparer.Equals(list[i], other[i]))
                    return false;
            }
            return true;
        }

        #endregion

        #region Slice(获取列表指定范围的列表)

        /// <summary>
        /// 获取列表指定范围的列表
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="start">开始索引</param>
        /// <param name="end">结束索引</param>
        public static IList<T> Slice<T>(this IList<T> list, int? start, int? end) => Slice(list, start, end, null);

        /// <summary>
        /// 获取列表指定范围的列表
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="start">开始索引</param>
        /// <param name="end">结束索引</param>
        /// <param name="step">递增值</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static IList<T> Slice<T>(this IList<T> list, int? start, int? end,int? step)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));
            if (step == 0)
                throw new ArgumentException($"{nameof(step)} 不能为0");
            var result=new List<T>();
            if (list.Count == 0)
                return result;

            var s = step ?? 1;
            var startIndex = start ?? 0;
            var endIndex = end ?? list.Count;

            startIndex = (startIndex < 0) ? list.Count + startIndex : startIndex;
            endIndex = (endIndex < 0) ? list.Count + endIndex : endIndex;

            startIndex = Math.Max(startIndex, 0);
            endIndex = Math.Min(endIndex, list.Count - 1);

            for (var i = startIndex; i < endIndex; i += s)
                result.Add(list[i]);
            return result;
        }

        #endregion        
    }
}
