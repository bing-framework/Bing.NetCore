using System;
using System.Linq.Expressions;

namespace Bing.Datas.Sql.Queries.Builders.Abstractions
{
    /// <summary>
    /// Select子句
    /// </summary>
    public interface ISelectClause
    {
        /// <summary>
        /// 是否聚合操作
        /// </summary>
        bool IsAggregation { get; }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <param name="sqlBuilder">Sql生成器</param>
        /// <param name="register">实体别名注册器</param>
        /// <returns></returns>
        ISelectClause Clone(ISqlBuilder sqlBuilder, IEntityAliasRegister register);

        /// <summary>
        /// 过滤重复记录
        /// </summary>
        void Distinct();

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <param name="columnAlias">列别名</param>
        void Count(string columnAlias = null);

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <param name="column">列</param>
        /// <param name="columnAlias">列别名</param>
        void Count(string column, string columnAlias);

        /// <summary>
        /// 求总行数
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        void Count<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="column">列</param>
        /// <param name="columnAlias">列别名</param>
        void Sum(string column, string columnAlias = null);

        /// <summary>
        /// 求和
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        void Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 求平均值
        /// </summary>
        /// <param name="column">列</param>
        /// <param name="columnAlias">列别名</param>
        void Avg(string column, string columnAlias = null);

        /// <summary>
        /// 求平均值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        void Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 求最大值
        /// </summary>
        /// <param name="column">列</param>
        /// <param name="columnAlias">列别名</param>
        void Max(string column, string columnAlias = null);

        /// <summary>
        /// 求最大值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        void Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 求最小值
        /// </summary>
        /// <param name="column">列</param>
        /// <param name="columnAlias">列别名</param>
        void Min(string column, string columnAlias = null);

        /// <summary>
        /// 求最小值
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        void Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名</param>
        /// <param name="tableAlias">表别名</param>
        void Select(string columns, string tableAlias = null);

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
        void Select<TEntity>(Expression<Func<TEntity, object[]>> expression, bool propertyAsAlias = false)
            where TEntity : class;

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="columnAlias">列别名</param>
        void Select<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class;

        /// <summary>
        /// 设置子查询列
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="columnAlias">列别名</param>
        void Select(ISqlBuilder builder, string columnAlias);

        /// <summary>
        /// 设置子查询列
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="columnAlias">列别名</param>
        void Select(Action<ISqlBuilder> action, string columnAlias);

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        void AppendSql(string sql);

        /// <summary>
        /// 输出Sql
        /// </summary>
        /// <returns></returns>
        string ToSql();
    }
}
