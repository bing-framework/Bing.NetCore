using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // Select(设置列名)
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Select(string columns, string tableAlias = null)
        {
            SelectClause.Select(columns, tableAlias);
            return this;
        }

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">列名，范例：t => new object[] { t.Id, t.Name }</param>
        /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias = false) where TEntity : class
        {
            SelectClause.Select(columns, propertyAsAlias);
            return this;
        }

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null) where TEntity : class
        {
            SelectClause.Select(column, columnAlias);
            return this;
        }

        /// <summary>
        /// 设置子查询列
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Select(ISqlBuilder builder, string columnAlias)
        {
            SelectClause.Select(builder, columnAlias);
            return this;
        }

        /// <summary>
        /// 设置子查询列
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder Select(Action<ISqlBuilder> action, string columnAlias)
        {
            SelectClause.Select(action, columnAlias);
            return this;
        }

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        public virtual ISqlBuilder AppendSelect(string sql)
        {
            SelectClause.AppendSql(sql);
            return this;
        }
    }
}
