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
    /// <summary>当前查询图中的根来源。</summary>
    IReadOnlyList<TableSource> Sources { get; }

    /// <summary>追加类型化根来源。</summary>
    void AppendRoot(Type entityType, string alias = null, string schema = null);

    /// <summary>设置类型化派生表根来源。</summary>
    void From<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class;

    /// <summary>解析多源条件。</summary>
    ICondition ResolveMultiSourcePredicate(LambdaExpression expression, IReadOnlyList<TableSource> sources);

    /// <summary>使用指定参数管理器解析多源条件。</summary>
    ICondition ResolveMultiSourcePredicate(LambdaExpression expression, IReadOnlyList<TableSource> sources,
        IParameterManager parameterManager);

    /// <summary>解析多源列。</summary>
    IReadOnlyList<string> ResolveMultiSourceColumns(LambdaExpression expression, IReadOnlyList<TableSource> sources);

    /// <summary>解析多源 DTO 投影并返回投影成员。</summary>
    IReadOnlyList<string> ResolveMultiSourceDtoColumns(LambdaExpression expression,
        IReadOnlyList<TableSource> sources, out IReadOnlyCollection<string> projectedMembers);

    /// <summary>解析单来源值条件。</summary>
    ICondition ResolveMultiSourceValueCondition(LambdaExpression expression, TableSource source, object value,
        Operator @operator);

    /// <summary>合并多源解析产生的新参数。</summary>
    void MergeNewParameters(IParameterManager parameterManager);
}

/// <summary>
/// Lambda 多源查询的 Select 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceSelectClause : ISelectClause
{
    /// <summary>追加已完成来源绑定的投影 SQL。</summary>
    void AppendBoundColumns(string columns);

    /// <summary>追加带来源别名的聚合投影。</summary>
    void Aggregate<TEntity>(SqlAggregateFunction function, Expression<Func<TEntity, object>> expression,
        string tableAlias, string columnAlias, bool distinct) where TEntity : class;
}

/// <summary>
/// Lambda 多源查询的 Group By 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceGroupByClause : IGroupByClause
{
    /// <summary>追加已完成来源绑定的分组列。</summary>
    void AppendBoundColumns(IEnumerable<string> columns);

    /// <summary>设置已完成来源绑定的 Having 条件。</summary>
    void SetBoundHaving(ICondition condition);
}

/// <summary>
/// Lambda 多源查询的 Order By 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceOrderByClause : IOrderByClause
{
    /// <summary>追加已完成来源绑定的排序列。</summary>
    void AppendBoundColumns(IEnumerable<string> columns, bool desc);
}

/// <summary>
/// Lambda 多源查询的 Join 能力。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlMultiSourceJoinClause : IJoinClause
{
    /// <summary>当前查询图中的类型化连接来源。</summary>
    IReadOnlyList<TableSource> TypedSources { get; }

    /// <summary>添加类型化实体内连接。</summary>
    void Join<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>添加类型化实体左连接。</summary>
    void LeftJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>添加类型化实体右连接。</summary>
    void RightJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>添加类型化实体全连接。</summary>
    void FullJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class;

    /// <summary>添加类型化派生表内连接。</summary>
    void Join<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>添加类型化派生表左连接。</summary>
    void LeftJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>添加类型化派生表右连接。</summary>
    void RightJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>添加类型化派生表全连接。</summary>
    void FullJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class;

    /// <summary>添加类型化派生表交叉连接。</summary>
    void CrossJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class;
}
