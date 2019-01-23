using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 设置列名
    /// </summary>
    public interface ISelect
    {
        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名，范例：a.AppId As Id,a.Name</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        ISqlBuilder Select(string columns, string tableAlias = null);

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">列名，范例：t => new object[] { t.Id, t.Name }</param>
        /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
        /// <returns></returns>
        ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias = false)
            where TEntity : class;

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">列名，范例：t => t.Name</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Select(ISqlBuilder builder, string columnAlias);

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Select(Action<ISqlBuilder> action, string columnAlias);

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendSelect(string sql);
    }
}
