using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 子句工厂。
/// </summary>
public interface ISqlClauseFactory
{
    /// <summary>
    /// 为指定 Builder 上下文创建 Select 子句。
    /// </summary>
    /// <param name="context">绑定 Provider、运行状态和共享服务的子句上下文。</param>
    /// <returns>可配置选择列和聚合的 Select 子句。</returns>
    ISelectClause CreateSelect(SqlClauseContext context);

    /// <summary>
    /// 为指定 Builder 上下文创建 From 子句。
    /// </summary>
    /// <param name="context">绑定 Provider、运行状态和共享服务的子句上下文。</param>
    /// <returns>可配置查询源的 From 子句。</returns>
    IFromClause CreateFrom(SqlClauseContext context);

    /// <summary>
    /// 为指定 Builder 上下文创建 Join 子句。
    /// </summary>
    /// <param name="context">绑定 Provider、运行状态和共享服务的子句上下文。</param>
    /// <returns>可配置关联表和关联条件的 Join 子句。</returns>
    IJoinClause CreateJoin(SqlClauseContext context);

    /// <summary>
    /// 为指定 Builder 上下文创建 Where 子句。
    /// </summary>
    /// <param name="context">绑定 Provider、运行状态和共享服务的子句上下文。</param>
    /// <returns>可配置筛选条件的 Where 子句。</returns>
    IWhereClause CreateWhere(SqlClauseContext context);

    /// <summary>
    /// 为指定 Builder 上下文创建 Group By 子句。
    /// </summary>
    /// <param name="context">绑定 Provider、运行状态和共享服务的子句上下文。</param>
    /// <returns>可配置分组和 Having 条件的 Group By 子句。</returns>
    IGroupByClause CreateGroupBy(SqlClauseContext context);

    /// <summary>
    /// 为指定 Builder 上下文创建 Order By 子句。
    /// </summary>
    /// <param name="context">绑定 Provider、运行状态和共享服务的子句上下文。</param>
    /// <returns>可配置排序规则的 Order By 子句。</returns>
    IOrderByClause CreateOrderBy(SqlClauseContext context);
}