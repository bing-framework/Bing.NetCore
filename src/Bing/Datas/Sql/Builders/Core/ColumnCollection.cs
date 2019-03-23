using System;
using System.Linq;
using Bing.Utils.Extensions;

namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// 列集合
    /// </summary>
    public class ColumnCollection
    {
        #region 属性

        /// <summary>
        /// 列集合
        /// </summary>
        public string Columns { get; }

        /// <summary>
        /// 表别名
        /// </summary>
        public string TableAlias { get; }

        /// <summary>
        /// 表实体类型
        /// </summary>
        public Type TableType { get; }

        /// <summary>
        /// 是否使用原始值
        /// </summary>
        public bool Raw { get; }

        /// <summary>
        /// 是否聚合函数
        /// </summary>
        public bool IsAggregation { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="ColumnCollection"/>类型的实例
        /// </summary>
        /// <param name="columns">列集合</param>
        /// <param name="tableAlias">表别名</param>
        /// <param name="tableType">表实体类型</param>
        /// <param name="raw">是否使用原始值</param>
        /// <param name="isAggregation">是否聚合函数</param>
        public ColumnCollection(string columns, string tableAlias = null, Type tableType = null, bool raw = false, bool isAggregation = false)
        {
            Columns = columns;
            TableAlias = tableAlias;
            TableType = tableType;
            Raw = raw;
            IsAggregation = isAggregation;
        }

        #endregion

        #region ToSql(获取列名列表)

        /// <summary>
        /// 获取列名列表
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="register">实体别名注册器</param>
        /// <returns></returns>
        public string ToSql(IDialect dialect, IEntityAliasRegister register)
        {
            if (Raw)
            {
                return Columns;
            }

            if (IsAggregation)
            {
                return Columns;
            }

            var columns = Columns.Split(',').Select(column => new SqlItem(column, GetTableAlias(register))).ToList();
            return columns.Select(item => item.ToSql(dialect)).Join();
        }

        /// <summary>
        /// 获取表别名
        /// </summary>
        /// <param name="register">实体别名注册器</param>
        /// <returns></returns>
        private string GetTableAlias(IEntityAliasRegister register)
        {
            if (register != null && register.Contains(TableType))
            {
                return register.GetAlias(TableType);
            }

            return TableAlias;
        }

        #endregion
    }
}
