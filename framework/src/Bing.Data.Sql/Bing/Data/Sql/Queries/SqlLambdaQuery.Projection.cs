using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

public sealed partial class SqlLambdaQuery
{
    /// <summary>
    /// 设置查询排序。
    /// </summary>
    /// <remarks>
    /// 按单个来源的标量属性排序。
    /// </remarks>
    /// <param name="column">指定查询列的成员表达式或列引用。</param>
    /// <param name="desc">是否按降序排列；true 为降序，false 为升序。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false) where TEntity : class =>
        OrderBy(column, desc, null);

    /// <summary>
    /// 设置查询排序。
    /// </summary>
    /// <remarks>
    /// 按指定别名来源的标量属性排序。
    /// </remarks>
    /// <param name="column">指定查询列的成员表达式或列引用。</param>
    /// <param name="desc">是否按降序排列；true 为降序，false 为升序。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc, string alias) where TEntity : class =>
        _core.AtomicChange(() => { _core.OrderByCore(column, desc, ResolveSources(column, new[] { alias })); return this; });

    /// <summary>
    /// 将查询投影设置为选择所有数据源。
    /// </summary>
    /// <returns>当前 SQL Lambda 查询对象，可继续进行链式配置。</returns>
    public SqlLambdaQuery SelectAll() => _core.AtomicChange(() => { _core.SelectAllCore(); return this; });

    /// <summary>
    /// 从自动投影中排除指定列。
    /// </summary>
    /// <remarks>
    /// 从自动投影中排除指定实体的直接属性。
    /// </remarks>
    /// <param name="columns">从自动投影排除的直接成员表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ExcludeSelect<TEntity>(Expression<Func<TEntity, object>> columns) where TEntity : class =>
        ExcludeSelect(columns, null);

    /// <summary>
    /// 从自动投影中排除指定列。
    /// </summary>
    /// <remarks>
    /// 从自动投影的指定来源中排除直接属性。
    /// </remarks>
    /// <param name="columns">从自动投影排除的直接成员表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ExcludeSelect<TEntity>(Expression<Func<TEntity, object>> columns, string alias) where TEntity : class =>
        _core.AtomicChange(() => { _core.ExcludeCore(columns, alias); return this; });

    /// <summary>
    /// 从自动投影中排除指定列。
    /// </summary>
    /// <remarks>
    /// 从自动投影中排除一个属性名或 alias.列名引用。
    /// </remarks>
    /// <param name="column">待排除的属性名或 alias.列名引用。</param>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ExcludeSelect(string column) =>
        _core.AtomicChange(() => { _core.ExcludeCore(column); return this; });

    /// <summary>
    /// 设置查询投影。
    /// </summary>
    /// <remarks>
    /// 首次接管默认投影，之后累计单属性、匿名对象或 DTO 投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery Select<TEntity>(Expression<Func<TEntity, object>> projection) => Select(projection, null);

    /// <summary>
    /// 设置查询投影。
    /// </summary>
    /// <remarks>
    /// 累计指定来源的单属性、匿名对象或 DTO 投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery Select<TEntity>(Expression<Func<TEntity, object>> projection, string alias) =>
        Project(projection, false, false, new[] { alias });

    /// <summary>
    /// 设置查询投影。
    /// </summary>
    /// <remarks>
    /// 累计一个实体来源的 SQL 聚合和普通成员投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery Select<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, object>> projection) =>
        Select(projection, null);

    /// <summary>
    /// 设置查询投影。
    /// </summary>
    /// <remarks>
    /// 累计指定实体来源的 SQL 聚合投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery Select<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, object>> projection, string alias) =>
        Project(projection, false, true, new[] { alias });

    /// <summary>
    /// 设置查询分组。
    /// </summary>
    /// <remarks>
    /// 按单属性或匿名对象中的直接成员分组。
    /// </remarks>
    /// <param name="columns">用于分组的直接成员表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery GroupBy<TEntity>(Expression<Func<TEntity, object>> columns) => GroupBy(columns, null);

    /// <summary>
    /// 设置查询分组。
    /// </summary>
    /// <remarks>
    /// 按指定来源的直接成员分组。
    /// </remarks>
    /// <param name="columns">用于分组的直接成员表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery GroupBy<TEntity>(Expression<Func<TEntity, object>> columns, string alias) => _core.AtomicChange(() =>
    {
        _core.GroupByCore(columns, ResolveSources(columns, new[] { alias }));
        return this;
    });

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 用新的实体全部列替换当前投影。
    /// </remarks>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>() where TEntity : class => ReplaceSelect<TEntity>(false);

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 用新的实体全部列替换当前投影，可使用属性结果名。
    /// </remarks>
    /// <param name="propertyAsAlias">是否使用实体属性名作为结果列别名。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>(bool propertyAsAlias) where TEntity : class => _core.AtomicChange(() =>
    {
        _core.ApplyEntityProjection(ResolveSource<TEntity>(null), propertyAsAlias, true);
        return this;
    });

    /// <summary>
    /// 解析并应用 Lambda 投影表达式。
    /// </summary>
    /// <param name="expression">描述投影内容的 Lambda 表达式。</param>
    /// <param name="replace">是否替换已有投影。</param>
    /// <param name="functions">是否按函数投影规则解析表达式。</param>
    /// <param name="aliases">可选的数据源别名。</param>
    /// <returns>应用投影后的当前 Lambda 查询对象。</returns>
    private SqlLambdaQuery Project(LambdaExpression expression, bool replace, bool functions, string[] aliases = null) =>
        _core.AtomicChange(() =>
        {
            var sources = ResolveSources(expression, aliases, functions ? 1 : (int?)null);
            _core.ApplyProjection(expression, sources, replace, functions: functions);
            return this;
        });

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 以本次表达式原子替换当前显式或自动投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>(Expression<Func<TEntity, object>> projection) =>
        Project(projection, true, false);

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 按显式来源绑定并原子替换当前投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>(Expression<Func<TEntity, object>> projection, string alias) =>
        Project(projection, true, false, new[] { alias });

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 以本次表达式原子替换当前显式或自动投影。
    /// </remarks>
    /// <param name="columns">需要选择、排除或分组的直接成员表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>(Expression<Func<TEntity, object[]>> columns) =>
        Project(columns, true, false);

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 按显式来源绑定并原子替换当前投影。
    /// </remarks>
    /// <param name="columns">需要选择、排除或分组的直接成员表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>(Expression<Func<TEntity, object[]>> columns, string alias) =>
        Project(columns, true, false, new[] { alias });

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 以本次表达式原子替换当前显式或自动投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <typeparam name="TProjection">投影表达式的结果类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection) =>
        Project(projection, true, false);

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 按显式来源绑定并原子替换当前投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <typeparam name="TProjection">投影表达式的结果类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection, string alias) =>
        Project(projection, true, false, new[] { alias });

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 以本次表达式原子替换当前显式或自动投影。
    /// </remarks>
    /// <param name="columns">需要选择、排除或分组的直接成员表达式。</param>
    /// <typeparam name="TFirst">第一个查询来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个查询来源的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns) =>
        Project(columns, true, false);

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 按显式来源绑定并原子替换当前投影。
    /// </remarks>
    /// <param name="columns">需要选择、排除或分组的直接成员表达式。</param>
    /// <param name="firstAlias">第一个实体参数绑定的来源别名。</param>
    /// <param name="secondAlias">第二个实体参数绑定的来源别名。</param>
    /// <typeparam name="TFirst">第一个查询来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个查询来源的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns, string firstAlias, string secondAlias) =>
        Project(columns, true, false, new[] { firstAlias, secondAlias });

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 以本次表达式原子替换当前显式或自动投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <typeparam name="TFirst">第一个查询来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个查询来源的实体类型。</typeparam>
    /// <typeparam name="TProjection">投影表达式的结果类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TFirst, TSecond, TProjection>(Expression<Func<TFirst, TSecond, TProjection>> projection) =>
        Project(projection, true, false);

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 按显式来源绑定并原子替换当前投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <param name="firstAlias">第一个实体参数绑定的来源别名。</param>
    /// <param name="secondAlias">第二个实体参数绑定的来源别名。</param>
    /// <typeparam name="TFirst">第一个查询来源的实体类型。</typeparam>
    /// <typeparam name="TSecond">第二个查询来源的实体类型。</typeparam>
    /// <typeparam name="TProjection">投影表达式的结果类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TFirst, TSecond, TProjection>(Expression<Func<TFirst, TSecond, TProjection>> projection, string firstAlias, string secondAlias) =>
        Project(projection, true, false, new[] { firstAlias, secondAlias });

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 以本次表达式原子替换当前显式或自动投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, object>> projection) =>
        Project(projection, true, true);

    /// <summary>
    /// 替换当前查询的投影。
    /// </summary>
    /// <remarks>
    /// 按显式来源绑定并原子替换当前投影。
    /// </remarks>
    /// <param name="projection">定义结果列或结果对象的投影表达式。</param>
    /// <param name="alias">查询来源的显式别名；为 null 时按实体类型解析来源。</param>
    /// <typeparam name="TEntity">查询或身份规则关联的实体类型。</typeparam>
    /// <returns>当前查询对象。</returns>
    public SqlLambdaQuery ReplaceSelect<TEntity>(Expression<Func<TEntity, SqlAggregateFunctions<TEntity>, object>> projection, string alias) =>
        Project(projection, true, true, new[] { alias });
}
