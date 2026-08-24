using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// 已发布的一元泛型 Lambda 查询兼容基类。
/// </summary>
public abstract class SqlMultiLambdaQuery : SqlLambdaQuery
{
    private readonly SqlLambdaQuery _query;

    internal SqlMultiLambdaQuery(SqlLambdaQuery query) : base(query) =>
        _query = query ?? throw new ArgumentNullException(nameof(query));

    internal SqlLambdaQuery Query => _query;
    internal ISqlBuilder GetBuilder() => _query.GetBuilder();

    /// <summary>生成当前查询的 SQL 文本。</summary>
    public string ToSql() => _query.ToSql();

    /// <summary>同步物化查询结果。</summary>
    public List<TResult> ToList<TResult>(int? timeout = null) => _query.ToList<TResult>(timeout);

    /// <summary>同步获取第一行。</summary>
    public TResult First<TResult>(int? timeout = null) => _query.First<TResult>(timeout);

    /// <summary>同步获取第一行或默认值。</summary>
    public TResult FirstOrDefault<TResult>(int? timeout = null) => _query.FirstOrDefault<TResult>(timeout);

    /// <summary>同步获取唯一一行。</summary>
    public TResult Single<TResult>(int? timeout = null) => _query.Single<TResult>(timeout);

    /// <summary>同步获取唯一一行或默认值。</summary>
    public TResult SingleOrDefault<TResult>(int? timeout = null) => _query.SingleOrDefault<TResult>(timeout);

    /// <summary>同步获取标量结果。</summary>
    public TResult Scalar<TResult>(int? timeout = null) => _query.Scalar<TResult>(timeout);

    /// <summary>同步获取分页结果。</summary>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _query.ToPage<TResult>(pager, timeout);

    /// <summary>同步流式读取结果。</summary>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) => _query.AsEnumerable<TResult>(timeout);

    /// <summary>异步物化查询结果。</summary>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步获取第一行。</summary>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步获取第一行或默认值。</summary>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.FirstOrDefaultAsync<TResult>(timeout,
        cancellationToken);

    /// <summary>异步获取唯一一行。</summary>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步获取唯一一行或默认值。</summary>
    public Task<TResult> SingleOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.SingleOrDefaultAsync<TResult>(timeout,
        cancellationToken);

    /// <summary>异步获取标量结果。</summary>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步获取分页结果。</summary>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToPageAsync<TResult>(pager, timeout,
        cancellationToken);

    /// <summary>异步流式读取结果。</summary>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.AsAsyncEnumerable<TResult>(timeout,
        cancellationToken);

}

/// <summary>
/// 已发布的一元泛型 Lambda 查询兼容描述。
/// </summary>
/// <typeparam name="TEntity">查询来源实体类型。</typeparam>
public sealed class SqlLambdaQuery<TEntity> : SqlMultiLambdaQuery where TEntity : class
{
    internal SqlLambdaQuery(SqlLambdaQuery query) : base(query)
    {
    }

    /// <summary>设置默认实体投影。</summary>
    public SqlLambdaQuery<TEntity> Select(bool propertyAsAlias = false)
    {
        Query.LegacySelect<TEntity>(propertyAsAlias);
        return this;
    }

    /// <summary>设置实体属性数组投影。</summary>
    public SqlLambdaQuery<TEntity> Select(Expression<Func<TEntity, object[]>> columns) =>
        Select(columns, false);

    /// <summary>设置实体属性数组投影。</summary>
    public SqlLambdaQuery<TEntity> Select(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias)
    {
        Query.LegacySelect(columns, propertyAsAlias);
        return this;
    }

    /// <summary>设置单列投影。</summary>
    public SqlLambdaQuery<TEntity> Select(Expression<Func<TEntity, object>> column, string columnAlias = null)
    {
        Query.Select<TEntity, object>(column);
        return this;
    }

    /// <summary>设置 DTO 投影。</summary>
    public SqlLambdaQuery<TEntity> Select<TProjection>(Expression<Func<TEntity, TProjection>> projection)
    {
        Query.Select(projection);
        return this;
    }

    /// <summary>追加默认实体投影。</summary>
    public SqlLambdaQuery<TEntity> AppendSelect(bool propertyAsAlias = false)
    {
        Query.LegacyAppendSelect<TEntity>(propertyAsAlias);
        return this;
    }

    /// <summary>追加实体属性数组投影。</summary>
    public SqlLambdaQuery<TEntity> AppendSelect(Expression<Func<TEntity, object[]>> columns,
        bool propertyAsAlias = false)
    {
        Query.LegacyAppendSelect(columns, propertyAsAlias);
        return this;
    }

    /// <summary>追加单列投影。</summary>
    public SqlLambdaQuery<TEntity> AppendSelect(Expression<Func<TEntity, object>> column,
        string columnAlias = null)
    {
        Query.AppendSelect<TEntity, object>(column);
        return this;
    }

    /// <summary>清空投影。</summary>
    public SqlLambdaQuery<TEntity> ClearSelect()
    {
        Query.ClearSelect();
        return this;
    }

    /// <summary>启用去重投影。</summary>
    public SqlLambdaQuery<TEntity> Distinct()
    {
        Query.Distinct();
        return this;
    }

    /// <summary>追加实体条件。</summary>
    public SqlLambdaQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        Query.Where(predicate);
        return this;
    }

    /// <summary>追加实体参数条件。</summary>
    public SqlLambdaQuery<TEntity> Where<TValue>(Expression<Func<TEntity, TValue>> column, TValue value,
        Operator @operator = Operator.Equal)
    {
        Query.Where(column, value, @operator);
        return this;
    }

    /// <summary>按条件追加实体条件。</summary>
    public SqlLambdaQuery<TEntity> WhereIf(Expression<Func<TEntity, object>> column, object value, bool condition,
        Operator @operator = Operator.Equal)
    {
        Query.WhereIf(column, value, condition, @operator);
        return this;
    }

    /// <summary>按条件追加实体谓词。</summary>
    public SqlLambdaQuery<TEntity> WhereIf(Expression<Func<TEntity, bool>> predicate, bool condition)
    {
        Query.WhereIf(predicate, condition);
        return this;
    }

    /// <summary>创建实体聚合投影。</summary>
    public SqlLambdaQuery<TEntity> Aggregate(SqlAggregateFunction function,
        Expression<Func<TEntity, object>> column, string columnAlias = null, bool distinct = false)
    {
        Query.Aggregate(function, column, columnAlias, distinct);
        return this;
    }

    /// <summary>设置实体分组列。</summary>
    public SqlLambdaQuery<TEntity> GroupBy(Expression<Func<TEntity, object>> column)
    {
        Query.LegacyGroupBy(column);
        return this;
    }

    /// <summary>设置实体分组列。</summary>
    public SqlLambdaQuery<TEntity> GroupBy(params Expression<Func<TEntity, object>>[] columns)
    {
        Query.LegacyGroupBy(columns);
        return this;
    }

    /// <summary>设置实体排序列。</summary>
    public SqlLambdaQuery<TEntity> OrderBy(Expression<Func<TEntity, object>> column, bool desc = false)
    {
        Query.LegacyOrderBy(column, desc);
        return this;
    }

    /// <summary>跳过指定结果行。</summary>
    public SqlLambdaQuery<TEntity> Skip(int count)
    {
        Query.Skip(count);
        return this;
    }

    /// <summary>限制结果行数量。</summary>
    public SqlLambdaQuery<TEntity> Take(int count)
    {
        Query.Take(count);
        return this;
    }

}