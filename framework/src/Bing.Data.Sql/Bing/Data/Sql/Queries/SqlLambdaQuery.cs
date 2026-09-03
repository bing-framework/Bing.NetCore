using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// 使用方法级泛型表达式构建结构化 SQL 查询。
/// </summary>
/// <remarks>
/// 查询来源按调用顺序追加，表达式参数按来源实例绑定，不依赖来源数量生成公共类型。
/// </remarks>
public partial class SqlLambdaQuery : ISqlQueryBuilderAccessor
{
    /// <summary>
    /// 当前 Lambda 查询的内部执行核心。
    /// </summary>
    private readonly SqlLambdaQueryCore _core;

    /// <summary>
    /// 初始化一个 <see cref="SqlLambdaQuery"/> 类型的实例。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="builder">当前查询使用的 SQL 生成器。</param>
    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder)
    {
        _core = new SqlLambdaQueryCore(executor, builder);
    }

    /// <summary>
    /// 基于已有查询核心初始化一个查询描述副本。
    /// </summary>
    /// <param name="query">已有查询描述。</param>
    internal SqlLambdaQuery(SqlLambdaQuery query) => _core = query?._core ??
        throw new ArgumentNullException(nameof(query));

    /// <summary>
    /// 基于内部执行核心初始化一个查询描述。
    /// </summary>
    /// <param name="core">Lambda 查询执行核心。</param>
    private SqlLambdaQuery(SqlLambdaQueryCore core) => _core = core ?? throw new ArgumentNullException(nameof(core));

    /// <summary>
    /// 获取当前查询使用的 SQL 生成器。
    /// </summary>
    /// <returns>当前查询的 SQL 生成器。</returns>
    internal ISqlBuilder GetBuilder() => _core.GetBuilder();

    /// <summary>
    /// 获取当前查询上下文标识。
    /// </summary>
    internal string QueryContextId => _core.QueryContextId;

    /// <summary>
    /// 标记查询结构已变更。
    /// </summary>
    private void Touch() => _core.Touch();

    /// <summary>
    /// 获取查询的 From 子句。
    /// </summary>
    /// <param name="accessor">SQL 子句访问器。</param>
    /// <returns>当前查询的 From 子句。</returns>
    private static ISqlMultiSourceFromClause GetFromClause(ISqlQueryClauseAccessor accessor) =>
        SqlLambdaQueryCore.GetFromClause(accessor);

    /// <summary>
    /// 获取当前查询已绑定的表源。
    /// </summary>
    /// <param name="accessor">SQL 子句访问器。</param>
    /// <returns>已绑定的表源集合。</returns>
    private static IReadOnlyList<TableSource> GetBoundSources(ISqlQueryClauseAccessor accessor) =>
        SqlLambdaQueryCore.GetBoundSources(accessor);

    /// <inheritdoc />
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => GetBuilder();

    /// <inheritdoc />
    void ISqlQueryBuilderAccessor.MarkChanged() => Touch();

    /// <summary>
    /// 替换当前查询的投影列。
    /// </summary>
    /// <param name="columns">新的投影列 SQL 文本。</param>
    private void ReplaceSelect(string columns) => _core.ReplaceSelect(columns);

    /// <summary>
    /// 使用指定表源追加 Where 条件。
    /// </summary>
    /// <param name="expression">条件表达式。</param>
    /// <param name="sources">条件表达式绑定的表源。</param>
    private void WhereCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.WhereCore(expression, sources);

    /// <summary>
    /// 使用显式来源生成单列参数条件。
    /// </summary>
    /// <param name="column">返回条件列的表达式。</param>
    /// <param name="value">条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <param name="source">显式绑定的表源。</param>
    private void WhereValueCore(LambdaExpression column, object value, Operator @operator, TableSource source) =>
        _core.WhereValueCore(column, value, @operator, source);

    /// <summary>
    /// 追加嵌套条件组。
    /// </summary>
    /// <param name="configure">条件组配置委托。</param>
    private void WhereGroupCore(Action<ISqlConditionGroup> configure) => _core.WhereGroupCore(configure);

    /// <summary>
    /// 使用指定表源设置投影列。
    /// </summary>
    /// <param name="expression">投影表达式。</param>
    /// <param name="sources">投影表达式绑定的表源。</param>
    private void SelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.SelectCore(expression, sources);

    /// <summary>
    /// 使用指定表源设置类型化投影列。
    /// </summary>
    /// <param name="expression">类型化投影表达式。</param>
    /// <param name="sources">投影表达式绑定的表源。</param>
    private void SelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.SelectTypedCore(expression, sources);

    /// <summary>
    /// 使用指定表源追加投影列。
    /// </summary>
    /// <param name="expression">投影表达式。</param>
    /// <param name="sources">投影表达式绑定的表源。</param>
    private void AppendSelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.AppendSelectCore(expression, sources);

    /// <summary>
    /// 使用指定表源追加类型化投影列。
    /// </summary>
    /// <param name="expression">类型化投影表达式。</param>
    /// <param name="sources">投影表达式绑定的表源。</param>
    private void AppendSelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.AppendSelectTypedCore(expression, sources);

    /// <summary>
    /// 创建类型化派生表查询。
    /// </summary>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="expression">派生表投影表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <param name="sources">投影表达式绑定的表源。</param>
    /// <returns>类型化派生表描述。</returns>
    private SqlSubquery<TProjection> SelectSubqueryCore<TProjection>(LambdaExpression expression, string alias,
        IReadOnlyList<TableSource> sources)
        where TProjection : class => _core.SelectSubqueryCore<TProjection>(expression, alias, sources);

    /// <summary>
    /// 使用指定表源设置分组列。
    /// </summary>
    /// <param name="expression">分组列表达式。</param>
    /// <param name="sources">分组列表达式绑定的表源。</param>
    private void GroupByCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.GroupByCore(expression, sources);

    /// <summary>
    /// 使用指定表源设置排序列。
    /// </summary>
    /// <param name="expression">排序列表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <param name="sources">排序列表达式绑定的表源。</param>
    private void OrderByCore(LambdaExpression expression, bool desc, IReadOnlyList<TableSource> sources) =>
        _core.OrderByCore(expression, desc, sources);

    /// <summary>
    /// 使用指定表源设置 Having 条件。
    /// </summary>
    /// <param name="expression">Having 条件表达式。</param>
    /// <param name="sources">条件表达式绑定的表源。</param>
    private void HavingCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.HavingCore(expression, sources);

    /// <summary>
    /// 使用指定左侧来源添加类型化内连接。
    /// </summary>
    /// <typeparam name="TJoin">待连接的右侧实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    /// <param name="alias">右侧表别名。</param>
    /// <param name="schema">右侧表架构名。</param>
    private void JoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.JoinCore<TJoin>(predicate, leftSource, alias, schema);

    /// <summary>
    /// 使用指定左侧来源添加类型化左外连接。
    /// </summary>
    /// <typeparam name="TJoin">待连接的右侧实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    /// <param name="alias">右侧表别名。</param>
    /// <param name="schema">右侧表架构名。</param>
    private void LeftJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.LeftJoinCore<TJoin>(predicate, leftSource, alias, schema);

    /// <summary>
    /// 使用指定左侧来源添加类型化右外连接。
    /// </summary>
    /// <typeparam name="TJoin">待连接的右侧实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    /// <param name="alias">右侧表别名。</param>
    /// <param name="schema">右侧表架构名。</param>
    private void RightJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.RightJoinCore<TJoin>(predicate, leftSource, alias, schema);

    /// <summary>
    /// 使用指定左侧来源添加类型化全外连接。
    /// </summary>
    /// <typeparam name="TJoin">待连接的右侧实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    /// <param name="alias">右侧表别名。</param>
    /// <param name="schema">右侧表架构名。</param>
    private void FullJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.FullJoinCore<TJoin>(predicate, leftSource, alias, schema);

    /// <summary>
    /// 按指定左侧来源添加类型化派生表内连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    private void JoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.JoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    /// <summary>
    /// 按指定左侧来源添加类型化派生表左外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    private void LeftJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.LeftJoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    /// <summary>
    /// 按指定左侧来源添加类型化派生表右外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    private void RightJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.RightJoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    /// <summary>
    /// 按指定左侧来源添加类型化派生表全外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">左侧表源。</param>
    private void FullJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.FullJoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    /// <summary>
    /// 添加类型化交叉连接表。
    /// </summary>
    /// <typeparam name="TEntity">待连接的实体类型。</typeparam>
    /// <param name="alias">表别名。</param>
    /// <param name="schema">表架构名。</param>
    private void CrossJoinCore<TEntity>(string alias, string schema) where TEntity : class =>
        _core.CrossJoinCore<TEntity>(alias, schema);

    /// <summary>
    /// 添加类型化派生表交叉连接。
    /// </summary>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    private void CrossJoinCore<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        _core.CrossJoinCore(subquery);

    /// <summary>
    /// 设置查询跳过的结果行数。
    /// </summary>
    /// <param name="count">跳过的行数。</param>
    private void SkipCore(int count) => _core.SkipCore(count);

    /// <summary>
    /// 设置查询返回的最大结果行数。
    /// </summary>
    /// <param name="count">返回的最大行数。</param>
    private void TakeCore(int count) => _core.TakeCore(count);

    /// <summary>
    /// 追加实体来源。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="alias">表别名；传入 null 时由映射和来源注册逻辑生成。</param>
    /// <param name="schema">表架构名；传入 null 时使用 Provider 默认架构。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery From<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).AppendRoot(typeof(TEntity), alias, schema);
        Touch();
        return this;
    }

    /// <summary>
    /// 追加类型化派生表来源。
    /// </summary>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">要追加的派生表查询。</param>
    /// <returns>当前查询描述。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="subquery"/> 为 null 时抛出。</exception>
    public SqlLambdaQuery FromSubquery<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class
    {
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).From(subquery);
        Touch();
        return this;
    }

    /// <summary>
    /// 设置单来源投影。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回投影列的表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TEntity>(Expression<Func<TEntity, object[]>> columns)
    {
        SelectCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>按来源别名设置单来源投影。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回投影列的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TEntity>(Expression<Func<TEntity, object[]>> columns, string alias)
    {
        SelectCore(columns, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 设置单来源 DTO 投影。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TProjection">投影结果类型。</typeparam>
    /// <param name="projection">返回投影对象的表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection)
    {
        SelectTypedCore(projection, ResolveSources(projection));
        return this;
    }

    /// <summary>按来源别名设置单来源 DTO 投影。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TProjection">投影结果类型。</typeparam>
    /// <param name="projection">返回投影对象的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection,
        string alias)
    {
        SelectTypedCore(projection, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 设置双来源投影。
    /// </summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="columns">返回投影列的双来源表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        SelectCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>按两个来源别名设置双来源投影。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="columns">返回投影列的双来源表达式。</param>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns,
        string firstAlias, string secondAlias)
    {
        SelectCore(columns, ResolveTwoSources<TFirst, TSecond>(firstAlias, secondAlias));
        return this;
    }

    /// <summary>
    /// 设置双来源 DTO 投影。
    /// </summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <typeparam name="TProjection">投影结果类型。</typeparam>
    /// <param name="projection">返回投影对象的双来源表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TFirst, TSecond, TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection)
    {
        SelectTypedCore(projection, ResolveSources(projection));
        return this;
    }

    /// <summary>按两个来源别名设置双来源 DTO 投影。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <typeparam name="TProjection">投影结果类型。</typeparam>
    /// <param name="projection">返回投影对象的双来源表达式。</param>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TFirst, TSecond, TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection, string firstAlias, string secondAlias)
    {
        SelectTypedCore(projection, ResolveTwoSources<TFirst, TSecond>(firstAlias, secondAlias));
        return this;
    }

    /// <summary>使用单来源 DTO 投影创建类型化派生表。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="projection">返回派生表列的表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <returns>类型化派生表描述。</returns>
    public SqlSubquery<TProjection> SelectSubquery<TEntity, TProjection>(
        Expression<Func<TEntity, TProjection>> projection, string alias)
        where TProjection : class
    {
        return SelectSubqueryCore<TProjection>(projection, alias, ResolveSources(projection));
    }

    /// <summary>按来源别名使用单来源 DTO 投影创建类型化派生表。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="projection">返回派生表列的表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <param name="sourceAlias">来源表别名。</param>
    /// <returns>类型化派生表描述。</returns>
    public SqlSubquery<TProjection> SelectSubquery<TEntity, TProjection>(
        Expression<Func<TEntity, TProjection>> projection, string alias, string sourceAlias)
        where TProjection : class
    {
        return SelectSubqueryCore<TProjection>(projection, alias, new[] { ResolveSource<TEntity>(sourceAlias) });
    }

    /// <summary>使用双来源 DTO 投影创建类型化派生表。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="projection">返回派生表列的双来源表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <returns>类型化派生表描述。</returns>
    public SqlSubquery<TProjection> SelectSubquery<TFirst, TSecond, TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection, string alias)
        where TProjection : class
    {
        return SelectSubqueryCore<TProjection>(projection, alias, ResolveSources(projection));
    }

    /// <summary>按两个来源别名使用 DTO 投影创建类型化派生表。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="projection">返回派生表列的双来源表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <returns>类型化派生表描述。</returns>
    public SqlSubquery<TProjection> SelectSubquery<TFirst, TSecond, TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection, string alias,
        string firstAlias, string secondAlias)
        where TProjection : class
    {
        return SelectSubqueryCore<TProjection>(projection, alias,
            ResolveTwoSources<TFirst, TSecond>(firstAlias, secondAlias));
    }

    /// <summary>
    /// 设置实体默认投影。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="propertyAsAlias">是否将属性名作为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Select<TEntity>(bool propertyAsAlias = false) where TEntity : class
    {
        ResolveSource<TEntity>(null);
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        builder.ReplaceSelect(select => select.Select<TEntity>(propertyAsAlias));
        Touch();
        return this;
    }

    /// <summary>
    /// 追加单来源投影。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回追加列的表达式；传入 null 时使用默认投影行为。</param>
    /// <param name="propertyAsAlias">是否将属性名作为列别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery AppendSelect<TEntity>(Expression<Func<TEntity, object[]>> columns,
        bool propertyAsAlias = false)
        where TEntity : class
    {
        if (columns != null)
            ResolveSources(columns);
        GetBuilder().Select(columns, propertyAsAlias);
        Touch();
        return this;
    }

    /// <summary>按来源别名追加投影列。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回追加列的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery AppendSelect<TEntity>(Expression<Func<TEntity, object[]>> columns, string alias)
        where TEntity : class
    {
        AppendSelectCore(columns, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 追加单来源 DTO 投影列。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TProjection">投影结果类型。</typeparam>
    /// <param name="projection">返回追加投影对象的表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery AppendSelect<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection)
        where TEntity : class
    {
        AppendSelectTypedCore(projection, ResolveSources(projection));
        return this;
    }

    /// <summary>按来源别名追加单来源 DTO 投影列。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TProjection">投影结果类型。</typeparam>
    /// <param name="projection">返回追加投影对象的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery AppendSelect<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection,
        string alias)
        where TEntity : class
    {
        AppendSelectTypedCore(projection, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 启用投影去重。
    /// </summary>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Distinct()
    {
        ((ISqlQueryClauseAccessor)GetBuilder()).SelectClause.Distinct();
        Touch();
        return this;
    }

    /// <summary>使用单来源属性创建聚合投影。</summary>
    /// <typeparam name="TEntity">聚合列所属的实体类型。</typeparam>
    /// <param name="function">要执行的聚合函数。</param>
    /// <param name="column">返回聚合列的实体成员表达式。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>当前查询描述。</returns>
    /// <exception cref="NotSupportedException">当前 SQL Builder 不支持聚合投影替换时抛出。</exception>
    public SqlLambdaQuery Aggregate<TEntity>(SqlAggregateFunction function,
        Expression<Func<TEntity, object>> column, string columnAlias = null, bool distinct = false)
        where TEntity : class
    {
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        ResolveSource<TEntity>(null);
        builder.ReplaceSelect(select => select.Aggregate(function, column, columnAlias, distinct));
        Touch();
        return this;
    }

    /// <summary>
    /// 按来源别名追加单来源聚合投影。
    /// </summary>
    /// <typeparam name="TEntity">聚合列所属的实体类型。</typeparam>
    /// <param name="function">要执行的聚合函数。</param>
    /// <param name="column">返回聚合列的实体成员表达式。</param>
    /// <param name="alias">要绑定的查询来源别名。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <returns>当前查询描述。</returns>
    /// <exception cref="InvalidOperationException">未找到唯一匹配的实体来源时抛出。</exception>
    public SqlLambdaQuery Aggregate<TEntity>(SqlAggregateFunction function,
        Expression<Func<TEntity, object>> column, string alias, string columnAlias, bool distinct = false)
        where TEntity : class
    {
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        var source = ResolveSource<TEntity>(alias);
        builder.ReplaceSelect(select => ((ISqlMultiSourceSelectClause)select).Aggregate(function, column,
            source.Alias, columnAlias, distinct));
        Touch();
        return this;
    }

    /// <summary>
    /// 追加单来源条件。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="predicate">返回条件的表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TEntity>(Expression<Func<TEntity, bool>> predicate)
    {
        WhereCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>按来源别名追加单来源条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="predicate">返回条件的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias)
    {
        WhereCore(predicate, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 追加双来源条件。
    /// </summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="predicate">返回条件的双来源表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        WhereCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>按两个来源别名追加双来源条件。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="predicate">返回条件的双来源表达式。</param>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
    {
        WhereCore(predicate, ResolveTwoSources<TFirst, TSecond>(firstAlias, secondAlias));
        return this;
    }

    /// <summary>
    /// 追加单来源参数条件。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TValue">条件值类型。</typeparam>
    /// <param name="column">返回条件列的表达式。</param>
    /// <param name="value">条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TEntity, TValue>(Expression<Func<TEntity, TValue>> column, TValue value,
        Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var selector = Expression.Lambda<Func<TEntity, object>>(Expression.Convert(column.Body, typeof(object)),
            column.Parameters);
        WhereValueCore(selector, value, @operator, ResolveSource<TEntity>(null));
        return this;
    }

    /// <summary>按来源别名追加单来源参数条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <typeparam name="TValue">条件值类型。</typeparam>
    /// <param name="column">返回条件列的表达式。</param>
    /// <param name="value">条件值。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Where<TEntity, TValue>(Expression<Func<TEntity, TValue>> column, TValue value,
        string alias, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var selector = Expression.Lambda<Func<TEntity, object>>(Expression.Convert(column.Body, typeof(object)),
            column.Parameters);
        WhereValueCore(selector, value, @operator, ResolveSource<TEntity>(alias));
        return this;
    }

    /// <summary>
    /// 按条件追加单来源条件。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="condition">是否追加条件。</param>
    /// <param name="predicate">返回条件的表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery WhereIf<TEntity>(bool condition, Expression<Func<TEntity, bool>> predicate)
    {
        if (condition)
            Where(predicate);
        return this;
    }

    /// <summary>按条件和来源别名追加单来源条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="condition">是否追加条件。</param>
    /// <param name="predicate">返回条件的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery WhereIf<TEntity>(bool condition, Expression<Func<TEntity, bool>> predicate, string alias)
    {
        if (condition)
            Where(predicate, alias);
        return this;
    }

    /// <summary>
    /// 按条件追加单来源参数条件。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="condition">是否追加条件。</param>
    /// <param name="column">返回条件列的表达式。</param>
    /// <param name="value">条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery WhereIf<TEntity>(bool condition, Expression<Func<TEntity, object>> column, object value,
        Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (condition)
            WhereValueCore(column, value, @operator, ResolveSource<TEntity>(null));
        return this;
    }

    /// <summary>按条件和来源别名追加单来源参数条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="condition">是否追加条件。</param>
    /// <param name="column">返回条件列的表达式。</param>
    /// <param name="value">条件值。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery WhereIf<TEntity>(bool condition, Expression<Func<TEntity, object>> column, object value,
        string alias, Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (condition)
            Where(column, value, alias, @operator);
        return this;
    }

    /// <summary>
    /// 以嵌套 And/Or 条件组追加过滤条件。
    /// </summary>
    /// <param name="configure">配置嵌套条件组的委托。</param>
    /// <returns>追加条件组后的当前查询。</returns>
    public SqlLambdaQuery WhereGroup(Action<ISqlConditionGroup> configure)
    {
        WhereGroupCore(configure);
        return this;
    }

    /// <summary>
    /// 添加内连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="rightAlias">右侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Join<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string rightAlias = null)
        where TLeft : class where TRight : class
    {
        JoinCore<TRight>(predicate, ResolveSource<TLeft>(null), rightAlias, null);
        return this;
    }

    /// <summary>
    /// 使用高级来源选项添加内连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="options">连接来源选项。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Join<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        SqlJoinOptions options)
        where TLeft : class where TRight : class
    {
        JoinCore<TRight>(predicate, ResolveSource<TLeft>(options.LeftAlias), options.RightAlias, options.Schema);
        return this;
    }

    /// <summary>
    /// 添加左外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="rightAlias">右侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery LeftJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string rightAlias = null)
        where TLeft : class where TRight : class
    {
        LeftJoinCore<TRight>(predicate, ResolveSource<TLeft>(null), rightAlias, null);
        return this;
    }

    /// <summary>
    /// 使用高级来源选项添加左外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="options">连接来源选项。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery LeftJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        SqlJoinOptions options)
        where TLeft : class where TRight : class
    {
        LeftJoinCore<TRight>(predicate, ResolveSource<TLeft>(options.LeftAlias), options.RightAlias, options.Schema);
        return this;
    }

    /// <summary>
    /// 添加右外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="rightAlias">右侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery RightJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string rightAlias = null)
        where TLeft : class where TRight : class
    {
        RightJoinCore<TRight>(predicate, ResolveSource<TLeft>(null), rightAlias, null);
        return this;
    }

    /// <summary>
    /// 使用高级来源选项添加右外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="options">连接来源选项。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery RightJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        SqlJoinOptions options)
        where TLeft : class where TRight : class
    {
        RightJoinCore<TRight>(predicate, ResolveSource<TLeft>(options.LeftAlias), options.RightAlias, options.Schema);
        return this;
    }

    /// <summary>
    /// 添加全外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="rightAlias">右侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery FullJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string rightAlias = null)
        where TLeft : class where TRight : class
    {
        FullJoinCore<TRight>(predicate, ResolveSource<TLeft>(null), rightAlias, null);
        return this;
    }

    /// <summary>
    /// 使用高级来源选项添加全外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TRight">右侧来源实体类型。</typeparam>
    /// <param name="predicate">返回 Join 条件的双来源表达式。</param>
    /// <param name="options">连接来源选项。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery FullJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        SqlJoinOptions options)
        where TLeft : class where TRight : class
    {
        FullJoinCore<TRight>(predicate, ResolveSource<TLeft>(options.LeftAlias), options.RightAlias, options.Schema);
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表内连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftAlias">左侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Join<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        JoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表左外连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftAlias">左侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery LeftJoin<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        LeftJoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表右外连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftAlias">左侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery RightJoin<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        RightJoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表全外连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftAlias">左侧来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery FullJoin<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        FullJoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>
    /// 添加交叉连接。
    /// </summary>
    /// <typeparam name="TEntity">待连接的实体类型。</typeparam>
    /// <param name="alias">表别名。</param>
    /// <param name="schema">表架构名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery CrossJoin<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        CrossJoinCore<TEntity>(alias, schema);
        return this;
    }

    /// <summary>添加类型化派生表交叉连接。</summary>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">类型化派生表查询。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery CrossJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class
    {
        CrossJoinCore(subquery);
        return this;
    }

    /// <summary>
    /// 设置双来源分组列。
    /// </summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="columns">返回分组列的双来源表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery GroupBy<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        GroupByCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>按两个来源别名设置双来源分组列。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="columns">返回分组列的双来源表达式。</param>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery GroupBy<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns,
        string firstAlias, string secondAlias)
    {
        GroupByCore(columns, ResolveTwoSources<TFirst, TSecond>(firstAlias, secondAlias));
        return this;
    }

    /// <summary>设置单来源分组列。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回分组列的表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery GroupBy<TEntity>(Expression<Func<TEntity, object[]>> columns)
    {
        GroupByCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>按来源别名设置单来源分组列。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回分组列的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery GroupBy<TEntity>(Expression<Func<TEntity, object[]>> columns, string alias)
    {
        GroupByCore(columns, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 设置双来源排序列。
    /// </summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="columns">返回排序列的双来源表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery OrderBy<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns,
        bool desc = false)
    {
        OrderByCore(columns, desc, ResolveSources(columns));
        return this;
    }

    /// <summary>按两个来源别名设置双来源排序列。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="columns">返回排序列的双来源表达式。</param>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery OrderBy<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns,
        string firstAlias, string secondAlias, bool desc = false)
    {
        OrderByCore(columns, desc, ResolveTwoSources<TFirst, TSecond>(firstAlias, secondAlias));
        return this;
    }

    /// <summary>设置单来源排序列。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回排序列的表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc, ResolveSources(columns));
        return this;
    }

    /// <summary>按来源别名设置单来源排序列。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="columns">返回排序列的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, object[]>> columns, string alias,
        bool desc = false)
    {
        OrderByCore(columns, desc, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 设置双来源 Having 条件。
    /// </summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="predicate">返回 Having 条件的双来源表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Having<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        HavingCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>按两个来源别名设置双来源 Having 条件。</summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="predicate">返回 Having 条件的双来源表达式。</param>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Having<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate,
        string firstAlias, string secondAlias)
    {
        HavingCore(predicate, ResolveTwoSources<TFirst, TSecond>(firstAlias, secondAlias));
        return this;
    }

    /// <summary>设置单来源 Having 条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="predicate">返回 Having 条件的表达式。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Having<TEntity>(Expression<Func<TEntity, bool>> predicate)
    {
        HavingCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>按来源别名设置单来源 Having 条件。</summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="predicate">返回 Having 条件的表达式。</param>
    /// <param name="alias">要绑定的来源别名。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Having<TEntity>(Expression<Func<TEntity, bool>> predicate, string alias)
    {
        HavingCore(predicate, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 跳过指定数量的结果行。
    /// </summary>
    /// <param name="count">跳过的行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制返回的结果行数量。
    /// </summary>
    /// <param name="count">返回的最大行数。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery Take(int count)
    {
        TakeCore(count);
        return this;
    }

    /// <summary>
    /// 按表达式参数位置解析当前查询中的来源。
    /// </summary>
    /// <param name="expression">需要解析来源的 Lambda 表达式。</param>
    /// <returns>与表达式参数顺序一致的表源集合。</returns>
    /// <exception cref="InvalidOperationException">表达式参数没有可用来源时抛出。</exception>
    private IReadOnlyList<TableSource> ResolveSources(LambdaExpression expression)
    {
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var available = GetBoundSources(accessor).ToList();
        var selected = new List<TableSource>(expression.Parameters.Count);
        foreach (var parameter in expression.Parameters)
        {
            var candidates = available.Where(item => item.EntityType == parameter.Type).ToList();
            if (candidates.Count != 1)
                throw new InvalidOperationException($"实体 {parameter.Type.Name} 的查询来源不唯一，请提供有效别名。");

            var source = candidates[0];
            if (selected.Contains(source))
                throw new InvalidOperationException($"表达式参数 {parameter.Name} 重复绑定同一查询来源。");
            selected.Add(source);
        }
        return selected;
    }

    /// <summary>
    /// 按实体类型和可选别名解析唯一查询来源。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="alias">来源别名；传入 null 时要求该实体仅有一个来源。</param>
    /// <returns>匹配的唯一表源。</returns>
    /// <exception cref="InvalidOperationException">来源不存在或不唯一时抛出。</exception>
    private TableSource ResolveSource<TEntity>(string alias)
        => ResolveSource(typeof(TEntity), alias);

    /// <summary>
    /// 按实体类型和可选别名解析唯一查询来源。
    /// </summary>
    /// <param name="entityType">来源实体类型。</param>
    /// <param name="alias">来源别名；传入 null 时要求该实体仅有一个来源。</param>
    /// <returns>匹配的唯一表源。</returns>
    /// <exception cref="InvalidOperationException">来源不存在或不唯一时抛出。</exception>
    private TableSource ResolveSource(Type entityType, string alias)
    {
        var sources = GetBoundSources((ISqlQueryClauseAccessor)GetBuilder())
            .Where(item => item.EntityType == entityType).ToList();
        if (string.IsNullOrWhiteSpace(alias) == false)
            sources = sources.Where(item => string.Equals(item.Alias, alias, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sources.Count != 1)
            throw new InvalidOperationException($"实体 {entityType.Name} 的查询来源不唯一，请提供有效别名。");
        return sources[0];
    }

    /// <summary>
    /// 按两个显式别名解析不同的查询来源。
    /// </summary>
    /// <typeparam name="TFirst">第一个来源实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个来源实体类型。</typeparam>
    /// <param name="firstAlias">第一个来源别名。</param>
    /// <param name="secondAlias">第二个来源别名。</param>
    /// <returns>按表达式参数顺序排列的两个不同表源。</returns>
    /// <exception cref="InvalidOperationException">来源不存在、不唯一或两个别名指向同一来源时抛出。</exception>
    private IReadOnlyList<TableSource> ResolveTwoSources<TFirst, TSecond>(string firstAlias, string secondAlias)
    {
        var first = ResolveSource<TFirst>(firstAlias);
        var second = ResolveSource<TSecond>(secondAlias);
        if (ReferenceEquals(first, second))
            throw new InvalidOperationException("双来源表达式参数不能绑定同一查询来源。");
        return new[] { first, second };
    }
}
