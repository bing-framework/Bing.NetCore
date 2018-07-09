using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
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
        /// <returns></returns>
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
        /// <returns></returns>
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
        /// <returns></returns>
        public static int IndexOf<T>(this IList<T> list, Func<T, bool> comparison)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (comparison(list[i]))
                {
                    return i;
                }
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
        /// <returns></returns>
        public static string Join<T>(this IList<T> list, char joinChar)
        {
            return list.Join(joinChar.ToString());
        }

        /// <summary>
        /// 将列表连接为字符串，根据指定的字符串进行连接
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="joinString">分割字符串</param>
        /// <returns></returns>
        public static string Join<T>(this IList<T> list, string joinString)
        {
            if (list == null || !list.Any())
            {
                return string.Empty;
            }
            StringBuilder sb=new StringBuilder();
            int listCount = list.Count;
            int listCountMinusOne = listCount - 1;

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
    }
}
