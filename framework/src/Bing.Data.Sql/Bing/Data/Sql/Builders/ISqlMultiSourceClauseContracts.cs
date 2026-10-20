using System.ComponentModel;
using System.Linq.Expressions;
using Bing.Data.Queries;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Lambda 多源查询的 From 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceFromClause : IFromClause
{
    /// <summary>
    /// 获取当前查询图中的根来源集合。
    /// </summary>
    IReadOnlyList<TableSource> Sources { get; }

    /// <summary>
    /// 追加一个类型化根来源。
    /// </summary>
    /// <param name="entityType">根来源对应的实体类型。</param>
    /// <param name="alias">根来源别名；未指定时使用默认别名。</param>
    /// <param name="schema">根来源架构；未指定时使用默认架构。</param>
    void AppendRoot(Type entityType, string alias = null, string schema = null);

    /// <summary>
    /// 将类型化派生表设置为根来源。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    void From<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class;

    /// <summary>
    /// 解析多源条件表达式。
    /// </summary>
    /// <param name="expression">要解析的条件表达式。</param>
    /// <param name="sources">按表达式参数顺序排列的表来源。</param>
    /// <returns>包含解析结果及参数的条件。</returns>
    ICondition ResolveMultiSourcePredicate(LambdaExpression expression, IReadOnlyList<TableSource> sources);

    /// <summary>
    /// 解析多源条件表达式。
    /// </summary>
    /// <param name="expression">要解析的条件表达式。</param>
    /// <param name="sources">按表达式参数顺序排列的表来源。</param>
    /// <param name="parameterManager">接收解析参数的参数管理器。</param>
    /// <returns>包含解析结果及参数的条件。</returns>
    ICondition ResolveMultiSourcePredicate(LambdaExpression expression, IReadOnlyList<TableSource> sources,
        IParameterManager parameterManager);

    /// <summary>
    /// 根据多个表来源解析列表达式。
    /// </summary>
    /// <param name="expression">要解析的列表达式。</param>
    /// <param name="sources">按表达式参数顺序排列的表来源。</param>
    /// <returns>按表达式顺序排列的列 SQL。</returns>
    IReadOnlyList<string> ResolveMultiSourceColumns(LambdaExpression expression, IReadOnlyList<TableSource> sources);

    /// <summary>
    /// 解析多源 DTO 投影并获取投影成员名称。
    /// </summary>
    /// <param name="expression">要解析的 DTO 投影表达式。</param>
    /// <param name="sources">按表达式参数顺序排列的表来源。</param>
    /// <param name="projectedMembers">接收投影成员名称的集合。</param>
    /// <returns>按投影顺序排列的列 SQL。</returns>
    IReadOnlyList<string> ResolveMultiSourceDtoColumns(LambdaExpression expression,
        IReadOnlyList<TableSource> sources, out IReadOnlyCollection<string> projectedMembers);

    /// <summary>
    /// 根据指定表来源解析成员与常量值之间的条件。
    /// </summary>
    /// <param name="expression">用于选择来源列的成员表达式。</param>
    /// <param name="source">表达式绑定的表来源。</param>
    /// <param name="value">参与比较的条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>包含来源列和参数值的条件。</returns>
    ICondition ResolveMultiSourceValueCondition(LambdaExpression expression, TableSource source, object value,
        Operator @operator);

    /// <summary>
    /// 合并多源解析过程中生成的参数。
    /// </summary>
    /// <param name="parameterManager">包含待合并参数的参数管理器。</param>
    void MergeNewParameters(IParameterManager parameterManager);
}

/// <summary>
/// Lambda 多源查询的 Select 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceSelectClause : ISelectClause
{
    /// <summary>
    /// 追加已完成来源绑定的投影 SQL。
    /// </summary>
    /// <param name="columns">已解析并包含来源别名的投影 SQL。</param>
    void AppendBoundColumns(string columns);

    /// <summary>
    /// 追加带来源别名的聚合投影。
    /// </summary>
    /// <typeparam name="TEntity">聚合表达式对应的实体类型。</typeparam>
    /// <param name="function">要使用的聚合函数。</param>
    /// <param name="expression">选择聚合列的表达式。</param>
    /// <param name="tableAlias">聚合列所属的表别名。</param>
    /// <param name="columnAlias">聚合结果别名。</param>
    /// <param name="distinct">是否仅聚合不同的列值。</param>
    void Aggregate<TEntity>(SqlAggregateFunction function, Expression<Func<TEntity, object>> expression,
        string tableAlias, string columnAlias, bool distinct) where TEntity : class;
}

/// <summary>
/// Lambda 多源查询的 Group By 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceGroupByClause : IGroupByClause
{
    /// <summary>
    /// 追加已完成来源绑定的分组列。
    /// </summary>
    /// <param name="columns">已解析并包含来源别名的分组列 SQL。</param>
    void AppendBoundColumns(IEnumerable<string> columns);

    /// <summary>
    /// 设置已完成来源绑定的 Having 条件。
    /// </summary>
    /// <param name="condition">已解析并完成参数绑定的 Having 条件。</param>
    void SetBoundHaving(ICondition condition);
}

/// <summary>
/// Lambda 多源查询的 Order By 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceOrderByClause : IOrderByClause
{
    /// <summary>
    /// 追加已完成来源绑定的排序列。
    /// </summary>
    /// <param name="columns">已解析并包含来源别名的排序列 SQL。</param>
    /// <param name="desc">是否按降序排列。</param>
    void AppendBoundColumns(IEnumerable<string> columns, bool desc);
}

/// <summary>
/// Lambda 多源查询的 Join 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceJoinClause : IJoinClause
{
    /// <summary>
    /// 获取当前查询图中的类型化连接来源集合。
    /// </summary>
    IReadOnlyList<TableSource> TypedSources { get; }

    /// <summary>
    /// 添加类型化内连接。
    /// </summary>
    /// <typeparam name="TEntity">要连接的实体类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    /// <param name="alias">连接来源别名；未指定时使用默认别名。</param>
    /// <param name="schema">连接来源架构；未指定时使用默认架构。</param>
    void Join<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>
    /// 添加类型化左外连接。
    /// </summary>
    /// <typeparam name="TEntity">要连接的实体类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    /// <param name="alias">连接来源别名；未指定时使用默认别名。</param>
    /// <param name="schema">连接来源架构；未指定时使用默认架构。</param>
    void LeftJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>
    /// 添加类型化右外连接。
    /// </summary>
    /// <typeparam name="TEntity">要连接的实体类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    /// <param name="alias">连接来源别名；未指定时使用默认别名。</param>
    /// <param name="schema">连接来源架构；未指定时使用默认架构。</param>
    void RightJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>
    /// 添加类型化全连接。
    /// </summary>
    /// <typeparam name="TEntity">要连接的实体类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    /// <param name="alias">连接来源别名；未指定时使用默认别名。</param>
    /// <param name="schema">连接来源架构；未指定时使用默认架构。</param>
    void FullJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>
    /// 添加类型化内连接。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="subquery">要连接的类型化派生表。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    void Join<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>
    /// 添加类型化左外连接。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="subquery">要连接的类型化派生表。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    void LeftJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>
    /// 添加类型化右外连接。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="subquery">要连接的类型化派生表。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    void RightJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>
    /// 添加类型化全连接。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
    /// <param name="fromClause">提供根来源的查询子句。</param>
    /// <param name="subquery">要连接的类型化派生表。</param>
    /// <param name="predicate">定义连接条件的表达式。</param>
    void FullJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>
    /// 添加类型化派生表交叉连接。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
    /// <param name="subquery">要连接的类型化派生表。</param>
    void CrossJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class;
}
