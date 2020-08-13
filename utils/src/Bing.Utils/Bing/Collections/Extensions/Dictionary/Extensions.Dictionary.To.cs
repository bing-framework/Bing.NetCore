using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Text;
using System.Web;

// ReSharper disable once CheckNamespace
namespace Bing.Collections
{
    /// <summary>
    /// 字典(<see cref="IDictionary{TKey,TValue}"/>) 扩展
    /// </summary>
    public static partial class DictionaryExtensions
    {
        /// <summary>
        /// 转换为有序字典
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        public static SortedDictionary<TKey, TValue> ToSortedDictionary<TKey, TValue>(this IDictionary<TKey, TValue> @this) => new SortedDictionary<TKey, TValue>(@this);

        /// <summary>
        /// 转换为有序字典
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="comparer">键比较器</param>
        public static SortedDictionary<TKey, TValue> ToSortedDictionary<TKey, TValue>(this IDictionary<TKey, TValue> @this, IComparer<TKey> comparer) =>
            new SortedDictionary<TKey, TValue>(@this, comparer);

        /// <summary>
        /// 转换为集合(名称-值)
        /// </summary>
        /// <param name="this"></param>
        public static NameValueCollection ToNameValueCollection(this IDictionary<string, string> @this)
        {
            if (@this == null)
                return null;
            var col = new NameValueCollection();
            foreach (var item in @this)
                col.Add(item.Key, item.Value);
            return col;
        }

        /// <summary>
        /// 转换为数据参数
        /// </summary>
        /// <param name="this">字典</param>
        /// <param name="command">数据库命令</param>
        public static IDbDataParameter[] ToDbParameters(this IDictionary<string, object> @this, IDbCommand command) =>
            @this.Select(x =>
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = x.Key;
                parameter.Value = x.Value;
                return parameter;
            }).ToArray();

        /// <summary>
        /// 转换为数据参数
        /// </summary>
        /// <param name="this">字典</param>
        /// <param name="connection">数据库连接</param>
        public static IDbDataParameter[] ToDbParameters(this IDictionary<string, object> @this, IDbConnection connection)
        {
            var command = connection.CreateCommand();
            return @this.Select(x =>
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = x.Key;
                parameter.Value = x.Value;
                return parameter;
            }).ToArray();
        }

        /// <summary>
        /// 转换为数据表
        /// </summary>
        /// <param name="this">字典</param>
        public static DataTable ToDataTable(this IDictionary<string, object> @this)
        {
            if (null == @this)
                throw new ArgumentNullException(nameof(@this));
            var dataTable = new DataTable();
            if (@this.Keys.Count == 0)
                return dataTable;
            dataTable.Columns.AddRange(@this.Keys.Select(key => new DataColumn(key, @this[key].GetType())).ToArray());
            foreach (var key in @this.Keys)
            {
                var row = dataTable.NewRow();
                row[key] = @this[key];
                dataTable.Rows.Add(row);
            }
            return dataTable;
        }

        /// <summary>
        /// 转换为字典
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">键值对集合</param>
        public static IDictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> @this) =>
            @this.ToDictionary(pair => pair.Key, pair => pair.Value);

        /// <summary>
        /// 转换为名称-值集合
        /// </summary>
        /// <param name="this">键值对集合</param>
        public static NameValueCollection ToNameValueCollection(this IEnumerable<KeyValuePair<string, string>> @this)
        {
            if (@this == null)
                return null;
            var col = new NameValueCollection();
            foreach (var item in @this)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                    continue;
                col.Add(item.Key, item.Value);
            }
            return col;
        }

        /// <summary>
        /// 转换为查询字符串
        /// </summary>
        /// <param name="this">键值对集合</param>
        public static string ToQueryString(this IEnumerable<KeyValuePair<string, string>> @this)
        {
            if (@this == null)
                return string.Empty;
            var sb = new StringBuilder(1024);
            foreach (var item in @this)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                    continue;
                sb.Append("&");
                sb.Append(HttpUtility.UrlEncode(item.Key));
                sb.Append("=");
                if (item.Value != null)
                    sb.Append(HttpUtility.UrlEncode(item.Value));
            }
            return sb.Length > 1 ? sb.ToString(1, sb.Length - 1) : "";
        }
    }
}
