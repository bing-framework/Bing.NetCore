using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// <see cref="IEnumerable{T}"/> 扩展
    /// </summary>
    public static class EnumerableExtensions
    {
        #region ForEach(对指定集合中的每个元素执行指定操作)

        /// <summary>
        /// 对指定集合中的每个元素执行指定操作
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="action">操作</param>
        public static void ForEach<T>(this IEnumerable<T> value, Action<T> action)
        {
            foreach (var item in value)
            {
                action(item);
            }
        }

        #endregion

        #region DistinctBy(根据指定条件返回集合中不重复的元素)

        /// <summary>
        /// 根据指定条件返回集合中不重复的元素
        /// </summary>
        /// <typeparam name="T">动态类型</typeparam>
        /// <typeparam name="TKey">动态筛选条件类型</typeparam>
        /// <param name="source">要操作的源</param>
        /// <param name="keySelector">重复数据筛选条件</param>
        /// <returns>不重复元素的集合</returns>
        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
        {
            keySelector.CheckNotNull("keySelector");
            source = source as IList<T> ?? source.ToList();
            return source.GroupBy(keySelector).Select(group => group.First());
        }

        #endregion

        #region ExpandAndToString(展开集合并转换为字符串)

        /// <summary>
        /// 将集合展开并分别转换成字符串，再以指定的分隔符衔接，拼成一个字符串返回。默认分隔符为逗号
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">要处理的结合</param>
        /// <param name="separator">分隔符，默认为逗号</param>
        /// <returns>拼接后的字符串</returns>
        public static string ExpandAndToString<T>(this IEnumerable<T> collection, string separator = ",")
        {
            return collection.ExpandAndToString(t => t.ToString(), separator);
        }

        /// <summary>
        /// 将集合展开并转为字符串，循环集合每一项，调用委托生成字符串，返回合并后的字符串。默认分隔符为逗号
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">要处理的集合</param>
        /// <param name="itemFormatFunc">单个集合项的转换委托</param>
        /// <param name="separator">分隔符，默认为逗号</param>
        /// <returns>拼接后的字符串</returns>
        public static string ExpandAndToString<T>(this IEnumerable<T> collection, Func<T, string> itemFormatFunc,
            string separator = ",")
        {
            collection = collection as IList<T> ?? collection.ToList();
            itemFormatFunc.CheckNotNull("itemFormatFunc");
            if (!collection.Any())
            {
                return null;
            }
            StringBuilder sb = new StringBuilder();
            int i = 0;
            int count = collection.Count();
            foreach (T t in collection)
            {
                if (i == count - 1)
                {
                    sb.Append(itemFormatFunc(t));
                }
                else
                {
                    sb.Append(itemFormatFunc(t) + separator);
                }
                i++;
            }
            return sb.ToString();
        }

        #endregion
    }
}
