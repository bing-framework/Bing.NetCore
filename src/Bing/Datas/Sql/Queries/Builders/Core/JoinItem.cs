using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Conditions;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    /// <summary>
    /// 表连接项
    /// </summary>
    public class JoinItem
    {
        /// <summary>
        /// 连接类型
        /// </summary>
        public string JoinType { get; }

        /// <summary>
        /// 表
        /// </summary>
        public SqlItem Table { get; }

        /// <summary>
        /// 连接条件列表
        /// </summary>
        public List<List<OnItem>> Conditions { get; }

        /// <summary>
        /// 初始化一个<see cref="JoinItem"/>类型的实例
        /// </summary>
        /// <param name="joinType">连接类型</param>
        /// <param name="table">表名</param>
        /// <param name="schema">架构名</param>
        /// <param name="alias">别名</param>
        /// <param name="raw">是否使用原始值</param>
        /// <param name="isSplit">是否用句点分割表名</param>
        public JoinItem(string joinType, string table, string schema = null, string alias = null, bool raw = false,
            bool isSplit = true)
        {
            JoinType = joinType;
            Table = new SqlItem(table, schema, alias, raw, isSplit);
            Conditions = new List<List<OnItem>>();
        }

        /// <summary>
        /// 初始化一个<see cref="JoinItem"/>类型的实例
        /// </summary>
        /// <param name="joinItem">表连接项</param>
        protected JoinItem(JoinItem joinItem)
        {
            JoinType = joinItem.JoinType;
            Table = joinItem.Table;
            Conditions = joinItem.Conditions.Select(t => new List<OnItem>(t)).ToList();
        }

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        public void On(string left, string right, Operator @operator)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return;
            }

            var conditions = GetFirstConditions();
            conditions.Add(new OnItem(left, right, @operator));
        }

        /// <summary>
        /// 获取第一个连接条件列表
        /// </summary>
        /// <returns></returns>
        private List<OnItem> GetFirstConditions()
        {
            var result = Conditions.FirstOrDefault();
            if (result == null)
            {
                result = new List<OnItem>();
                Conditions.Add(result);
            }
            return result;
        }

        /// <summary>
        /// 设置连接条件列表
        /// </summary>
        /// <param name="items">连接条件列表</param>
        public void On(List<OnItem> items)
        {
            if (items == null)
            {
                return;
            }
            Conditions.Add(items);
        }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public JoinItem Clone()
        {
            return new JoinItem(this);
        }

        /// <summary>
        /// 获取Join语句
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <returns></returns>
        public string ToSql(IDialect dialect = null)
        {
            var table = Table.ToSql(dialect);
            return $"{JoinType} {table}{GetOn(dialect)}";
        }

        /// <summary>
        /// 获取On语句
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <returns></returns>
        private string GetOn(IDialect dialect)
        {
            if (Conditions.Count == 0)
            {
                return null;
            }

            var result = new StringBuilder();
            Conditions.ForEach(items =>
            {
                On(items, result, dialect);
                result.Append(" Or ");
            });
            result.Remove(result.Length - 4, 4);
            return $" On {result}";
        }

        /// <summary>
        /// 获取单个连接条件
        /// </summary>
        /// <param name="items">连接条件列表</param>
        /// <param name="result">Sql拼接</param>
        /// <param name="dialect">Sql方言</param>
        private void On(List<OnItem> items, StringBuilder result, IDialect dialect)
        {
            items.ForEach(item =>
            {
                On(item,result,dialect);
                result.Append(" And ");
            });
            result.Remove(result.Length - 5, 5);
        }

        /// <summary>
        /// 获取单个连接条件
        /// </summary>
        /// <param name="item">连接条件项</param>
        /// <param name="result">Sql拼接</param>
        /// <param name="dialect">Sql方言</param>
        private void On(OnItem item, StringBuilder result, IDialect dialect)
        {
            var left = item.Left.ToSql(dialect);
            var right = item.Right.ToSql(dialect);
            result.Append(SqlConditionFactory.Create(left, right, item.Operator).GetCondition());
        }
    }
}
