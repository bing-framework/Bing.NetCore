using System.Reflection;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// WhereGroup 条件组原子提交测试。
/// </summary>
public class WhereGroupAtomicityTest
{
    /// <summary>
    /// 测试目的：既有 Where 与条件组组合时应保留既有条件，并按嵌套优先级生成完整 SQL 和参数。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenExistingWhereIsPresent_ShouldAppendGroupedCondition()
    {
        // Arrange
        var query = CreateQuery();
        query.Where<Sample>(item => item.IntValue == 1);

        // Act
        query.WhereGroup(group =>
        {
            group.And<Sample>(item => item.IntValue > 2);
            group.Or<Sample>(item => item.IntValue == 3);
            group.Group(nested => nested.And<Sample>(item => item.IntValue < 10));
        });

        // Assert
        Assert.Equal(
            "Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue]=@_p_0 And ([s].[IntValue]>@_p_1 Or [s].[IntValue]=@_p_2) And [s].[IntValue]<@_p_3",
            query.ToSql());
        Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2", "@_p_3" }, query.GetBuilder().GetParams().Keys.ToArray());
        Assert.Equal(new object[] { 1, 2, 3, 10 }, query.GetBuilder().GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：OrGroup 应以 Or 接入外层，并保留嵌套组内部的逻辑优先级。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenOrGroupIsNested_ShouldPreserveLogicalPrecedence()
    {
        // Arrange
        var query = CreateQuery();

        // Act
        query.WhereGroup(group =>
        {
            group.And<Sample>(item => item.IntValue == 1);
            group.OrGroup(nested =>
            {
                nested.And<Sample>(item => item.IntValue == 2);
                nested.Or<Sample>(item => item.IntValue == 3);
            });
        });

        // Assert
        Assert.Equal(
            "Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere ([s].[IntValue]=@_p_0 Or ([s].[IntValue]=@_p_1 Or [s].[IntValue]=@_p_2))",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：空条件组不应改变 SQL、参数、来源和 Shape 缓存状态。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenGroupIsEmpty_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var query = CreateQuery();
        query.Where<Sample>(item => item.IntValue == 1);
        var before = Capture(query);

        // Act
        query.WhereGroup(_ => { });
        var after = Capture(query);

        // Assert
        Assert.Equal(before, after);
    }

    /// <summary>
    /// 测试目的：条件组引用未知来源时应失败，且不得提交任何条件、参数或缓存版本变化。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenSourceIsUnknown_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var query = CreateQuery();
        query.Where<Sample>(item => item.IntValue == 1);
        var before = Capture(query);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.WhereGroup(group =>
            group.And<Sample2>(item => item.IntValue == 2)));
        var after = Capture(query);

        // Assert
        Assert.Contains("未找到表达式参数", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// 测试目的：条件组引用重复实体且未提供 alias 时应失败，不得依赖 Lambda 参数名选择来源。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenSameEntitySourceIsAmbiguousWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var query = CreateDuplicateQuery();
        var before = Capture(query);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.WhereGroup(group =>
            group.And<Sample>(renamed => renamed.IntValue == 1)));
        var after = Capture(query);

        // Assert
        Assert.Contains("查询来源不唯一", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// 测试目的：条件组单来源显式 alias 应绑定指定来源并生成完整 SQL。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenSingleSourceAliasIsExplicit_ShouldRenderCompleteSql()
    {
        // Arrange
        var query = CreateDuplicateQuery();

        // Act
        query.WhereGroup(group => group.And<Sample>(renamed => renamed.IntValue == 1, "right"));

        // Assert
        Assert.Equal(
            "Select [left].[IntValue] \r\nFrom [Sample] As [left], [Sample] As [right] \r\nWhere [right].[IntValue]=@_p_0",
            query.ToSql());
        Assert.Equal(new object[] { 1 }, query.GetBuilder().GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：条件组双来源显式 alias 应按 alias 而非 Lambda 参数名绑定完整比较条件。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenTwoSourceAliasesAreExplicit_ShouldRenderCompleteSql()
    {
        // Arrange
        var query = CreateDuplicateQuery();

        // Act
        query.WhereGroup(group => group.Or<Sample, Sample>(
            (renamedFirst, renamedSecond) => renamedFirst.IntValue == renamedSecond.IntValue,
            "left", "right"));

        // Assert
        Assert.Equal(
            "Select [left].[IntValue] \r\nFrom [Sample] As [left], [Sample] As [right] \r\nWhere [left].[IntValue]=[right].[IntValue]",
            query.ToSql());
        Assert.Empty(query.GetBuilder().GetParams());
    }

    /// <summary>
    /// 测试目的：查询来源出现重复 alias 时，条件组无法安全定位来源，失败后必须保持原状态。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenDuplicateAliasExists_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var query = CreateQuery();
        var before = Capture(query);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.From<Sample>("s"));
        var after = Capture(query);
        query.WhereGroup(group => group.And<Sample>(item => item.IntValue == 1));

        // Assert
        Assert.Contains("已存在表别名", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// 测试目的：条件组参数预检超过 Provider 上限时应失败，且不得污染原有参数和 SQL 状态。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenParameterLimitIsExceeded_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        var query = CreateQuery(new TestSqlBuilder(parameterManager: parameterManager));
        query.Where<Sample>(item => item.IntValue == 1);
        var before = Capture(query);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.WhereGroup(group =>
            group.And<Sample>(item => item.IntValue == 2)));
        var after = Capture(query);

        // Assert
        Assert.Contains("参数数量", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// 创建带有确定投影的非泛型 Lambda 查询描述。
    /// </summary>
    /// <param name="builder">查询使用的 SQL Builder。</param>
    /// <returns>待测试查询描述。</returns>
    private static SqlLambdaQuery CreateQuery(TestSqlBuilder builder = null) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder ?? new TestSqlBuilder())
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });

    private static SqlLambdaQuery CreateDuplicateQuery() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<Sample>("left")
            .From<Sample>("right")
            .Select<Sample>(item => new object[] { item.IntValue }, "left");

    /// <summary>
    /// 捕获条件组操作前后的可观察状态和实例 Shape 缓存状态。
    /// </summary>
    /// <param name="query">待捕获查询。</param>
    /// <returns>查询状态快照。</returns>
    private static QueryState Capture(SqlLambdaQuery query)
    {
        var sql = query.ToSql();
        var coreField = typeof(SqlLambdaQuery).GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic);
        var core = coreField.GetValue(query);
        var innerQueryField = core.GetType().GetField("_query", BindingFlags.Instance | BindingFlags.NonPublic);
        var innerQuery = innerQueryField.GetValue(core);
        var shapeVersion = GetField<long>(innerQuery, "_shapeVersion");
        var cachedVersion = GetField<long>(innerQuery, "_cachedVersion");
        var cachedSql = GetField<string>(innerQuery, "_cachedSql");
        var builder = query.GetBuilder();
        var parameters = builder.GetParams();
        return new QueryState(sql, parameters.Keys.ToArray(), parameters.Values.ToArray(), shapeVersion,
            cachedVersion, cachedSql);
    }

    /// <summary>
    /// 读取独立查询描述的私有字段。
    /// </summary>
    /// <typeparam name="T">字段类型。</typeparam>
    /// <param name="target">目标对象。</param>
    /// <param name="name">字段名称。</param>
    /// <returns>字段值。</returns>
    private static T GetField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field.GetValue(target);
    }

    /// <summary>
    /// 查询状态快照。
    /// </summary>
    private sealed class QueryState : IEquatable<QueryState>
    {
        public QueryState(string sql, string[] parameterNames, object[] parameterValues, long shapeVersion,
            long cachedVersion, string cachedSql)
        {
            Sql = sql;
            ParameterNames = parameterNames;
            ParameterValues = parameterValues;
            ShapeVersion = shapeVersion;
            CachedVersion = cachedVersion;
            CachedSql = cachedSql;
        }

        private string Sql { get; }
        private string[] ParameterNames { get; }
        private object[] ParameterValues { get; }
        private long ShapeVersion { get; }
        private long CachedVersion { get; }
        private string CachedSql { get; }
        public bool Equals(QueryState other)
        {
            if (other == null)
                return false;
            return Sql == other.Sql && ShapeVersion == other.ShapeVersion && CachedVersion == other.CachedVersion &&
                CachedSql == other.CachedSql && ParameterNames.SequenceEqual(other.ParameterNames) &&
                ParameterValues.SequenceEqual(other.ParameterValues);
        }

        public override bool Equals(object obj) => Equals(obj as QueryState);

        public override int GetHashCode() => HashCode.Combine(Sql, ShapeVersion, CachedVersion, CachedSql);
    }

}