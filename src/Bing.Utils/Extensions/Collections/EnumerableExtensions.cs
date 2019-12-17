using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Utils.Json;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 可枚举类型<see cref="IEnumerable{T}"/> 扩展
    /// </summary>
    public static class EnumerableExtensions
    {
        #region ForEach(对指定集合中的每个元素执行指定操作)

        /// <summary>
        /// 对指定集合中的每个元素执行指定操作
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="enumerable">值</param>
        /// <param name="action">操作</param>
        /// <exception cref="ArgumentNullException">源集合对象为空、操作表达式为空</exception>
        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
            if (action == null)
                throw new ArgumentNullException(nameof(action), @"操作表达式不可为空！");
            foreach (var item in enumerable)
                action(item);
        }

        /// <summary>
        /// 对指定集合中的每个元素执行指定操作
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="enumerable">值</param>
        /// <param name="action">操作</param>
        /// <exception cref="ArgumentNullException">源集合对象为空、操作表达式为空</exception>
        public static Task ForEachAsync<T>(this IEnumerable<T> enumerable, Func<T, Task> action)
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
            if (action == null)
                throw new ArgumentNullException(nameof(action), @"操作表达式不可为空！");
            return Task.WhenAll(from item in enumerable select Task.Run(() => action(item)));
        }

        #endregion

        #region EqualsTo(判断两个集合中的元素是否相等)

        /// <summary>
        /// 判断两个集合中的元素是否相等
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="sourceList">源集合</param>
        /// <param name="targetList">目标集合</param>
        /// <exception cref="ArgumentNullException">源集合对象为空、目标集合对象为空</exception>
        public static bool EqualsTo<T>(this IEnumerable<T> sourceList, IEnumerable<T> targetList)
        {
            if (sourceList == null)
                throw new ArgumentNullException(nameof(sourceList), $@"源{typeof(T).Name}集合对象不可为空！");
            if (targetList == null)
                throw new ArgumentNullException(nameof(targetList), $@"目标{typeof(T).Name}集合对象不可为空！");
            // 长度对比
            if (sourceList.Count() != targetList.Count())
                return false;
            if (!sourceList.Any() && !targetList.Any())
                return true;
            // 浅度对比
            if (!sourceList.Except(targetList).Any() && !targetList.Except(sourceList).Any())
                return true;
            // 深度对比
            var sourceListStr = sourceList.ToJson().Trim();
            var targetListStr = targetList.ToJson().Trim();
            if (sourceListStr == targetListStr)
                return true;
            return false;
        }

        #endregion

        #region DistinctBy(根据指定条件返回集合中不重复的元素)

        /// <summary>
        /// 根据指定条件返回集合中不重复的元素
        /// </summary>
        /// <typeparam name="T">动态类型</typeparam>
        /// <typeparam name="TKey">动态筛选条件类型</typeparam>
        /// <param name="enumerable">源集合</param>
        /// <param name="keySelector">字段选择委托</param>
        /// <exception cref="ArgumentNullException">源集合对象为空、参照字段表达式为空</exception>
        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> enumerable, Func<T, TKey> keySelector)
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector), @"参照字段表达式不可为空");
            enumerable = enumerable as IList<T> ?? enumerable.ToList();
            var seenKeys = new HashSet<TKey>();
            return enumerable.Where(item => seenKeys.Add(keySelector(item)));
        }

        #endregion

        #region ExpandAndToString(展开集合并转换为字符串)

        /// <summary>
        /// 将集合展开并分别转换成字符串，再以指定的分隔符衔接，拼成一个字符串返回。默认分隔符为逗号
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">要处理的集合</param>
        /// <param name="separator">分隔符，默认为逗号</param>
        /// <param name="wrapItem">项目包裹符</param>
        public static string ExpandAndToString<T>(this IEnumerable<T> collection, string separator = ",",
            string wrapItem = "") =>
            collection.ExpandAndToString(t => t.ToString(), separator, wrapItem);

        /// <summary>
        /// 将集合展开并转为字符串，循环集合每一项，调用委托生成字符串，返回合并后的字符串。默认分隔符为逗号
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">要处理的集合</param>
        /// <param name="itemFormatFunc">单个集合项的转换委托</param>
        /// <param name="separator">分隔符，默认为逗号</param>
        /// <param name="wrapItem">项目包裹符</param>
        public static string ExpandAndToString<T>(this IEnumerable<T> collection, Func<T, string> itemFormatFunc,
            string separator = ",", string wrapItem = "")
        {
            collection = collection as IList<T> ?? collection.ToList();
            itemFormatFunc.CheckNotNull(nameof(itemFormatFunc));
            if (!collection.Any())
                return null;
            var sb = new StringBuilder();
            var i = 0;
            var count = collection.Count();
            foreach (var t in collection)
            {
                if (!string.IsNullOrWhiteSpace(wrapItem))
                {
                    sb.Append(i == count - 1
                        ? $"{wrapItem}{itemFormatFunc(t)}{wrapItem}"
                        : $"{wrapItem}{itemFormatFunc(t)}{wrapItem}{separator}");
                }
                else
                {
                    if (i == count - 1)
                        sb.Append(itemFormatFunc(t));
                    else
                        sb.Append(itemFormatFunc(t) + separator);
                }
                i++;
            }
            return sb.ToString();
        }

        #endregion

        #region ToDataTable(转换为DataTable)

        /// <summary>
        /// 泛型集合转换为DataTable
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="enumerable">集合</param>
        /// <param name="tableName">表名</param>
        /// <exception cref="ArgumentNullException">源集合对象为空</exception>
        public static DataTable ToDataTable<T>(this IEnumerable<T> enumerable, string tableName = "")
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
            var type = typeof(T);
            var properties = type.GetProperties();
            var dataTable = string.IsNullOrEmpty(tableName) ? new DataTable() : new DataTable(tableName);
            foreach (var property in properties)
            {
                dataTable.Columns.Add(new DataColumn(property.Name));
            }

            var array = enumerable.ToArray();
            for (var i = 0; i < array.Length; i++)
            {
                foreach (var property in properties)
                {
                    dataTable.Rows[i][property.Name] = property.GetValue(array[i]);
                }
            }

            return dataTable;
        }

        #endregion

        #region WhereIf(是否执行条件查询)

        /// <summary>
        /// 是否执行指定条件的查询，根据第三方条件是否为真来决定
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="enumerable">源集合</param>
        /// <param name="predicate">查询条件</param>
        /// <param name="condition">第三方条件</param>
        public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate, bool condition)
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
            enumerable = enumerable as IList<T> ?? enumerable.ToList();
            return condition ? enumerable.Where(predicate) : enumerable;
        }

        #endregion
    }
}
