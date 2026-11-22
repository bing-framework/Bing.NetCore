using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

public sealed partial class SqlLambdaQuery
{
    /// <summary>
    /// 设置聚合筛选条件。
    /// </summary>
    /// <remarks>
    /// 使用 SQL 聚合或分组属性构造参数化 Having 条件。
    /// </remarks>
    /// <param name="predicate">定义聚合筛选条件的表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery Having<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, bool>> predicate) => Having(predicate, null);

    /// <summary>
    /// 设置聚合筛选条件。
    /// </summary>
    /// <remarks>
    /// 为指定来源构造聚合筛选条件。
    /// </remarks>
    /// <param name="predicate">定义聚合筛选条件的表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery Having<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, bool>> predicate, string alias) =>
        _core.AtomicChange(() =>
        {
            var sources = ResolveSources(predicate, new[] { alias }, 1);
            ((GroupByClause)((ISqlQueryClauseAccessor)GetBuilder()).GroupByClause).SetFunctionHaving(predicate, sources);
            Touch();
            return this;
        });

    /// <summary>
    /// 设置查询排序。
    /// </summary>
    /// <remarks>
    /// 按 SQL 聚合或分组属性升序排列。
    /// </remarks>
    /// <param name="key">定义排序键的成员或聚合表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, object>> key) => OrderBy(key, false);

    /// <summary>
    /// 设置查询排序。
    /// </summary>
    /// <remarks>
    /// 按 SQL 聚合或分组属性排列。
    /// </remarks>
    /// <param name="key">定义排序键的成员或聚合表达式。</param>
    /// <param name="desc">是否按降序排列；true 为降序，false 为升序。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, object>> key, bool desc) => OrderBy(key, desc, null);

    /// <summary>
    /// 设置查询排序。
    /// </summary>
    /// <remarks>
    /// 按指定来源的 SQL 聚合或分组属性排列。
    /// </remarks>
    /// <param name="key">定义排序键的成员或聚合表达式。</param>
    /// <param name="desc">是否按降序排列；true 为降序，false 为升序。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, object>> key, bool desc, string alias) =>
        _core.AtomicChange(() =>
        {
            var sources = ResolveSources(key, new[] { alias }, 1);
            ((OrderByClause)((ISqlQueryClauseAccessor)GetBuilder()).OrderByClause)
                .AddLogicalColumns(SqlFunctionProjectionParser.Parse(key, sources, true), desc);
            Touch();
            return this;
        });
}
