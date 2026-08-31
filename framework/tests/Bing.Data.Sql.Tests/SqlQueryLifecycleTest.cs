using System.Reflection;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 独立 SQL 查询描述生命周期测试。
/// </summary>
public class SqlQueryLifecycleTest
{
    /// <summary>
    /// 测试目的：ToSql 只渲染查询快照，不应冻结描述，后续合法修改仍应生效。
    /// </summary>
    [Fact]
    public void ToSql_WhenQueryIsDraft_ShouldNotFreezeDescription()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var query = CreateQuery(executor);

        // Act
        var beforeWhere = query.ToSql();
        query.Where<Sample>(item => item.IntValue == 7);
        var afterWhere = query.ToSql();

        // Assert
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s]", beforeWhere);
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue]=@_p_0",
            afterWhere);
        Assert.NotEqual(beforeWhere, afterWhere);
    }

    /// <summary>
    /// 测试目的：同一查询形状重复渲染应命中实例缓存，结构成功变更后必须失效并重新渲染。
    /// </summary>
    [Fact]
    public void ToSql_WhenShapeIsUnchanged_ShouldReuseCachedSqlUntilMutation()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });

        // Act
        var first = query.ToSql();
        var second = query.ToSql();
        query.Where<Sample>(item => item.IntValue == 8);
        var third = query.ToSql();

        // Assert
        Assert.Equal(first, second);
        Assert.NotEqual(second, third);
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：Raw Fluent 扩展在首次 ToSql 后追加条件时必须失效查询缓存，并保持执行参数与 SQL 一致。
    /// </summary>
    [Fact]
    public void RawFluent_WhenWhereIsAddedAfterToSql_ShouldInvalidateCachedSql()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");

        // Act
        var beforeSql = query.ToSql();
        query.Where("Status", 1);
        var afterSql = query.ToSql();
        var parameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Orders]", beforeSql);
        Assert.Equal("Select [Id] \r\nFrom [Orders] \r\nWhere [Status]=@_p_0", afterSql);
        Assert.Equal(new[] { "@_p_0" }, parameters.Keys.ToArray());
        Assert.Equal(new object[] { 1 }, parameters.Values.ToArray());
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：Raw Fluent 空白追加操作必须保持 SQL、参数和缓存版本不变。
    /// </summary>
    [Fact]
    public void RawFluent_WhenBlankAppendIsUsed_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);

        // Act
        query.AppendWhere("   ");
        var afterSql = query.ToSql();

        // Assert
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(beforeShapeVersion, GetFluentShapeVersion(query));
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
        Assert.Equal(1, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：公开 WhereIfNotEmpty 在空值或空白值时不得使已缓存的 Fluent SQL 失效。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RawFluent_WhenWhereIfNotEmptyValueIsBlank_ShouldKeepQueryStateUnchanged(string value)
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);
        var beforeCachedVersion = GetFluentQueryField<long>(query, "_cachedVersion");

        // Act
        query.WhereIfNotEmpty("Status", value);
        var afterSql = query.ToSql();

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Orders]", beforeSql);
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(beforeShapeVersion, GetFluentShapeVersion(query));
        Assert.Equal(beforeCachedVersion, GetFluentQueryField<long>(query, "_cachedVersion"));
        Assert.Empty(query.GetParams());
        Assert.Equal(1, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：公开 WhereIfNotEmpty 在非空值时必须只使 Fluent SQL 缓存失效一次。
    /// </summary>
    [Fact]
    public void RawFluent_WhenWhereIfNotEmptyValueExists_ShouldInvalidateCacheOnce()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);

        // Act
        query.WhereIfNotEmpty("Status", "active");
        var afterSql = query.ToSql();

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Orders]", beforeSql);
        Assert.Equal("Select [Id] \r\nFrom [Orders] \r\nWhere [Status]=@_p_0", afterSql);
        Assert.Equal(beforeShapeVersion + 1, GetFluentShapeVersion(query));
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys.ToArray());
        Assert.Equal(new object[] { "active" }, query.GetParams().Values.ToArray());
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：Raw Fluent 查询在首次渲染后追加 Union 时必须失效缓存并渲染新的完整 SQL。
    /// </summary>
    [Fact]
    public void RawFluent_WhenUnionIsAddedAfterToSql_ShouldInvalidateCachedSql()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var union = builder.New().Select("Id").From("ArchivedOrders");
        var beforeSql = query.ToSql();

        // Act
        query.Union(union);
        var afterSql = query.ToSql();

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Orders]", beforeSql);
        Assert.Equal("(Select [Id] \r\nFrom [Orders] \r\n) \r\nUnion \r\n(Select [Id] \r\nFrom [ArchivedOrders] \r\n)",
            afterSql);
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：Raw Fluent 查询在首次渲染后追加 CTE 时必须失效缓存并渲染新的完整 SQL。
    /// </summary>
    [Fact]
    public void RawFluent_WhenCteIsAddedAfterToSql_ShouldInvalidateCachedSql()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("active_orders");
        var cte = builder.New().Select("Id").From("Orders");
        var beforeSql = query.ToSql();

        // Act
        query.With("active_orders", cte);
        var afterSql = query.ToSql();

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [active_orders]", beforeSql);
        Assert.Equal("With [active_orders] \r\nAs (Select [Id] \r\nFrom [Orders])\r\nSelect [Id] \r\nFrom [active_orders]",
            afterSql);
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：Raw Fluent 查询在首次渲染后绑定参数时必须失效缓存，并保留 SQL 与参数的一致状态。
    /// </summary>
    [Fact]
    public void RawFluent_WhenParameterIsAddedAfterToSql_ShouldInvalidateCachedSql()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders")
            .AppendWhere("Status=@status");
        var beforeSql = query.ToSql();

        // Act
        query.AddParam("status", 1);
        var afterSql = query.ToSql();
        var parameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Orders] \r\nWhere Status=@status", beforeSql);
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(1, parameters["@status"]);
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：清空已有参数必须通过 mutation gateway 失效缓存，并让后续执行看到空参数快照。
    /// </summary>
    [Fact]
    public void RawFluent_WhenParametersAreClearedAfterToSql_ShouldInvalidateCachedSqlAndSnapshot()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders")
            .AppendWhere("Status=@status")
            .AddParam("status", 1);
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);
        var beforeCachedVersion = GetFluentQueryField<long>(query, "_cachedVersion");

        // Act
        query.ClearParams();
        var afterSql = query.ToSql();
        var parameters = query.GetParams();
        var executionSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(query.GetBuilder());

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Orders] \r\nWhere Status=@status", beforeSql);
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(afterSql, executionSnapshot.Sql);
        Assert.Empty(executionSnapshot.Parameters);
        Assert.Equal(beforeShapeVersion + 1, GetFluentShapeVersion(query));
        Assert.NotEqual(beforeCachedVersion, GetFluentQueryField<long>(query, "_cachedVersion"));
        Assert.Empty(parameters);
        Assert.Equal(3, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：清空空参数集合必须是 no-op，不得改变版本、缓存或渲染次数。
    /// </summary>
    [Fact]
    public void RawFluent_WhenParametersAreAlreadyEmpty_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);
        var beforeCachedVersion = GetFluentQueryField<long>(query, "_cachedVersion");

        // Act
        query.ClearParams();
        var afterSql = query.ToSql();

        // Assert
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(beforeShapeVersion, GetFluentShapeVersion(query));
        Assert.Equal(beforeCachedVersion, GetFluentQueryField<long>(query, "_cachedVersion"));
        Assert.Empty(query.GetParams());
        Assert.Equal(1, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：Union 的空输入、CTE 的空名称和参数的空名称均不得改变查询缓存状态。
    /// </summary>
    [Fact]
    public void RawFluent_WhenCompositeMutationIsNoOp_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);

        // Act
        query.Union(Array.Empty<ISqlBuilder>());
        query.With("   ", builder.New().Select("Id").From("Orders"));
        query.AddParam("   ", 1);
        var afterSql = query.ToSql();

        // Assert
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(beforeShapeVersion, GetFluentShapeVersion(query));
        Assert.Equal(1, builder.Counters.ToSqlCallCount);
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：Union 子 Builder 克隆失败时不得提交联合项、参数或缓存版本变化。
    /// </summary>
    [Fact]
    public void RawFluent_WhenUnionCloneFails_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);
        var beforeCachedVersion = GetFluentQueryField<long>(query, "_cachedVersion");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.Union(new ThrowingCloneBuilder("union clone failed")));

        // Assert
        Assert.Equal("union clone failed", exception.Message);
        Assert.Equal(beforeSql, query.ToSql());
        Assert.Equal(beforeShapeVersion, GetFluentShapeVersion(query));
        Assert.Equal(beforeCachedVersion, GetFluentQueryField<long>(query, "_cachedVersion"));
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
        Assert.Equal(1, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：CTE 子 Builder 克隆失败时不得提交 CTE 项或缓存版本变化。
    /// </summary>
    [Fact]
    public void RawFluent_WhenCteCloneFails_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);
        var beforeCachedVersion = GetFluentQueryField<long>(query, "_cachedVersion");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            query.With("active_orders", new ThrowingCloneBuilder("cte clone failed")));

        // Assert
        Assert.Equal("cte clone failed", exception.Message);
        Assert.Equal(beforeSql, query.ToSql());
        Assert.Equal(beforeShapeVersion, GetFluentShapeVersion(query));
        Assert.Equal(beforeCachedVersion, GetFluentQueryField<long>(query, "_cachedVersion"));
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
        Assert.Equal(1, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：参数数量上限导致 AddParam 失败时不得写入参数或使查询缓存失效。
    /// </summary>
    [Fact]
    public void RawFluent_WhenParameterLimitIsExceeded_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var builder = new CountingTestSqlBuilder(parameterManager);
        var query = SqlQueryRuntimeFactory.CreateQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .Select("Id")
            .From("Orders")
            .AppendWhere("Status=@status");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetFluentShapeVersion(query);
        var beforeCachedVersion = GetFluentQueryField<long>(query, "_cachedVersion");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.AddParam("status", 1));

        // Assert
        Assert.Contains("参数数量超出上限", exception.Message, StringComparison.Ordinal);
        Assert.Equal(beforeSql, query.ToSql());
        Assert.Equal(beforeShapeVersion, GetFluentShapeVersion(query));
        Assert.Equal(beforeCachedVersion, GetFluentQueryField<long>(query, "_cachedVersion"));
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
        Assert.Equal(1, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：同类型多来源必须通过显式 alias 绑定，且 Lambda 参数名称变化不应改变 SQL 来源解析结果。
    /// </summary>
    [Fact]
    public void Lambda_WhenSameEntitySourcesUseExplicitAliases_ShouldBindStableSources()
    {
        // Arrange
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new TestSqlBuilder())
            .From<Sample>("left")
            .From<Sample>("right")
            .Select<Sample>(ignored => new object[] { ignored.IntValue }, "right")
            .Where<Sample, int>(value => value.IntValue, 7, "left");

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Equal(
            "Select [right].[IntValue] \r\nFrom [Sample] As [left], [Sample] As [right] \r\nWhere [left].[IntValue]=@_p_0",
            sql);
    }

    /// <summary>
    /// 测试目的：双来源表达式应按显式来源顺序绑定，支持同类型自连接而不依赖参数变量名。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSameEntitySourcesUseExplicitAliases_ShouldBindExpressionParameters()
    {
        // Arrange
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new TestSqlBuilder())
            .From<Sample>("parent")
            .From<Sample>("child")
            .Select<Sample, Sample>((firstValue, secondValue) =>
                new object[] { firstValue.IntValue, secondValue.IntValue }, "parent", "child")
            .Where<Sample, Sample>((firstValue, secondValue) =>
                firstValue.IntValue == secondValue.IntValue, "parent", "child");

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Equal(
            "Select [parent].[IntValue],[child].[IntValue] \r\nFrom [Sample] As [parent], [Sample] As [child] \r\nWhere [parent].[IntValue]=[child].[IntValue]",
            sql);
    }

    /// <summary>
    /// 测试目的：二元显式来源的投影、条件、分组和排序应按 alias 绑定不同来源，并渲染完整 SQL。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceAliasesAreExplicit_ShouldRenderCompleteSqlAcrossClauses()
    {
        // Arrange
        var query = CreateLambdaQuery()
            .From<Sample>("left")
            .From<Sample>("right")
            .Select<Sample, Sample>((firstValue, secondValue) =>
                new object[] { firstValue.IntValue, secondValue.IntValue }, "left", "right")
            .Where<Sample, Sample>((firstValue, secondValue) =>
                firstValue.IntValue == secondValue.IntValue, "left", "right")
            .GroupBy<Sample, Sample>((firstValue, secondValue) =>
                new object[] { firstValue.IntValue, secondValue.IntValue }, "left", "right")
            .OrderBy<Sample, Sample>((firstValue, secondValue) =>
                new object[] { secondValue.IntValue }, "left", "right");

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Equal(
            "Select [left].[IntValue],[right].[IntValue] \r\nFrom [Sample] As [left], [Sample] As [right] \r\nWhere [left].[IntValue]=[right].[IntValue] \r\nGroup By [left].[IntValue],[right].[IntValue] \r\nOrder By [right].[IntValue]",
            sql);
    }

    /// <summary>
    /// 测试目的：二元显式 Select 列投影不能将两个 Lambda 参数绑定到同一表源。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceSelectAliasesAreDuplicated_ShouldKeepQueryStateUnchanged()
    {
        AssertDuplicateExplicitAliasBindingFails(query => query.Select<Sample, Sample>(
            (firstValue, secondValue) => new object[] { firstValue.IntValue, secondValue.IntValue },
            "left", "left"));
    }

    /// <summary>
    /// 测试目的：二元显式 DTO 投影不能将两个 Lambda 参数绑定到同一表源。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceTypedSelectAliasesAreDuplicated_ShouldKeepQueryStateUnchanged()
    {
        AssertDuplicateExplicitAliasBindingFails(query => query.Select<Sample, Sample, SampleProjection>(
            (firstValue, secondValue) => new SampleProjection { IntValue = firstValue.IntValue },
            "left", "left"));
    }

    /// <summary>
    /// 测试目的：二元显式 SelectSubquery 来源不能将两个 Lambda 参数绑定到同一表源。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceSelectSubqueryAliasesAreDuplicated_ShouldKeepQueryStateUnchanged()
    {
        AssertDuplicateExplicitAliasBindingFails(query =>
        {
            _ = query.SelectSubquery<Sample, Sample, SampleProjection>(
                (firstValue, secondValue) => new SampleProjection { IntValue = firstValue.IntValue },
                "summary", "left", "left");
        });
    }

    /// <summary>
    /// 测试目的：二元显式 Where 来源不能将两个 Lambda 参数绑定到同一表源。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceWhereAliasesAreDuplicated_ShouldKeepQueryStateUnchanged()
    {
        AssertDuplicateExplicitAliasBindingFails(query => query.Where<Sample, Sample>(
            (firstValue, secondValue) => firstValue.IntValue == secondValue.IntValue, "left", "left"));
    }

    /// <summary>
    /// 测试目的：二元显式 GroupBy 来源不能将两个 Lambda 参数绑定到同一表源。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceGroupByAliasesAreDuplicated_ShouldKeepQueryStateUnchanged()
    {
        AssertDuplicateExplicitAliasBindingFails(query => query.GroupBy<Sample, Sample>(
            (firstValue, secondValue) => new object[] { firstValue.IntValue, secondValue.IntValue },
            "left", "left"));
    }

    /// <summary>
    /// 测试目的：二元显式 OrderBy 来源不能将两个 Lambda 参数绑定到同一表源。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceOrderByAliasesAreDuplicated_ShouldKeepQueryStateUnchanged()
    {
        AssertDuplicateExplicitAliasBindingFails(query => query.OrderBy<Sample, Sample>(
            (firstValue, secondValue) => new object[] { firstValue.IntValue, secondValue.IntValue },
            "left", "left"));
    }

    /// <summary>
    /// 测试目的：二元显式 Having 来源不能将两个 Lambda 参数绑定到同一表源。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceHavingAliasesAreDuplicated_ShouldKeepQueryStateUnchanged()
    {
        AssertDuplicateExplicitAliasBindingFails(query => query.Having<Sample, Sample>(
            (firstValue, secondValue) => firstValue.IntValue > secondValue.IntValue, "left", "left"));
    }

    /// <summary>
    /// 测试目的：同类型多来源聚合必须使用显式 alias，而不能绑定到最后注册的来源。
    /// </summary>
    [Fact]
    public void Lambda_WhenAggregateUsesExplicitAlias_ShouldBindSelectedSource()
    {
        // Arrange
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new TestSqlBuilder())
            .From<Sample>("left")
            .From<Sample>("right")
            .Aggregate<Sample>(SqlAggregateFunction.Sum, value => value.IntValue, "left", "Total");

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Equal(
            "Select Sum([left].[IntValue]) As [Total] \r\nFrom [Sample] As [left], [Sample] As [right]",
            sql);
    }

    /// <summary>
    /// 测试目的：显式 alias 缺失时应立即失败，且不得修改查询 SQL 或参数状态。
    /// </summary>
    [Fact]
    public void Lambda_WhenExplicitAliasIsMissing_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new TestSqlBuilder())
            .From<Sample>("source")
            .Select<Sample>(item => new object[] { item.IntValue });
        var beforeSql = query.ToSql();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.Where<Sample>(
            item => item.IntValue == 1, "missing"));

        // Assert
        Assert.Contains("查询来源不唯一", exception.Message, StringComparison.Ordinal);
        Assert.Equal(beforeSql, query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：同类型多来源用于 Where 且未提供 alias 时必须失败，不能按来源注册顺序静默绑定。
    /// </summary>
    [Fact]
    public void Lambda_WhenWhereUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.Where<Sample>(item => item.IntValue == 1));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于值型 Where 且未提供 alias 时必须失败，不能静默使用最后注册来源。
    /// </summary>
    [Fact]
    public void Lambda_WhenValueWhereUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.Where<Sample, int>(item => item.IntValue, 1));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于值型 IN 条件且未提供 alias 时必须失败，不能写入参数或条件。
    /// </summary>
    [Fact]
    public void Lambda_WhenValueWhereInUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.Where<Sample, object>(item => item.IntValue,
            new object[] { 1, 2 }, Operator.In));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于 WhereIf(true) 值条件且未提供 alias 时必须失败，不能静默绑定来源。
    /// </summary>
    [Fact]
    public void Lambda_WhenValueWhereIfUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.WhereIf(true, (Sample item) => item.IntValue, 1));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于默认实体投影且未提供 alias 时必须失败，不能冻结首个或最后一个来源。
    /// </summary>
    [Fact]
    public void Lambda_WhenDefaultSelectUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.Select<Sample>());
    }

    /// <summary>
    /// 测试目的：同类型多来源创建 SelectSubquery 且未提供来源 alias 时必须失败，并保持查询形状不变。
    /// </summary>
    [Fact]
    public void Lambda_WhenSelectSubqueryUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var query = CreateLambdaQuery()
            .From<Sample>("left")
            .From<Sample>("right")
            .Select<Sample>(item => new object[] { item.IntValue }, "left");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetShapeVersion(query);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.SelectSubquery<Sample, SampleProjection>(
            renamed => new SampleProjection { IntValue = renamed.IntValue }, "summary"));

        // Assert
        Assert.Contains("查询来源不唯一", exception.Message, StringComparison.Ordinal);
        Assert.Equal(beforeSql, query.ToSql());
        Assert.Equal(beforeShapeVersion, GetShapeVersion(query));
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
    }

    /// <summary>
    /// 测试目的：SelectSubquery 单来源显式 alias 应绑定指定来源并输出完整派生表 SQL。
    /// </summary>
    [Fact]
    public void Lambda_WhenSelectSubqueryUsesExplicitSourceAlias_ShouldRenderCompleteSql()
    {
        // Arrange
        var query = CreateLambdaQuery()
            .From<Sample>("left")
            .From<Sample>("right");

        // Act
        var summary = query.SelectSubquery<Sample, SampleProjection>(
            renamed => new SampleProjection { IntValue = renamed.IntValue }, "summary", "right");
        var outer = CreateLambdaQuery()
            .FromSubquery(summary)
            .Select<SampleProjection>(item => new object[] { item.IntValue });

        // Assert
        Assert.Equal(
            "Select [summary].[IntValue] \r\nFrom (Select [right].[IntValue] As [IntValue] \r\nFrom [Sample] As [left], [Sample] As [right]) As [summary]",
            outer.ToSql());
    }

    /// <summary>
    /// 测试目的：SelectSubquery 双来源显式 alias 应按 alias 而非 Lambda 参数名绑定完整投影 SQL。
    /// </summary>
    [Fact]
    public void Lambda_WhenTwoSourceSelectSubqueryAliasesAreExplicit_ShouldRenderCompleteSql()
    {
        // Arrange
        var query = CreateLambdaQuery()
            .From<Sample>("left")
            .From<Sample>("right");

        // Act
        var summary = query.SelectSubquery<Sample, Sample, SampleProjection>(
            (renamedFirst, renamedSecond) => new SampleProjection { IntValue = renamedSecond.IntValue },
            "summary", "left", "right");
        var outer = CreateLambdaQuery()
            .FromSubquery(summary)
            .Select<SampleProjection>(item => new object[] { item.IntValue });

        // Assert
        Assert.Equal(
            "Select [summary].[IntValue] \r\nFrom (Select [right].[IntValue] As [IntValue] \r\nFrom [Sample] As [left], [Sample] As [right]) As [summary]",
            outer.ToSql());
    }

    /// <summary>
    /// 测试目的：同类型多来源用于 Select 且未提供 alias 时必须失败，不能替换已有投影。
    /// </summary>
    [Fact]
    public void Lambda_WhenSelectUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.Select<Sample>(item => new object[] { item.IntValue }));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于 AppendSelect 且未提供 alias 时必须失败，不能追加错误列。
    /// </summary>
    [Fact]
    public void Lambda_WhenAppendSelectUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.AppendSelect<Sample>(
            item => new object[] { item.StringValue }));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于 GroupBy 且未提供 alias 时必须失败，不能修改分组状态。
    /// </summary>
    [Fact]
    public void Lambda_WhenGroupByUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.GroupBy<Sample>(
            item => new object[] { item.IntValue }));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于 OrderBy 且未提供 alias 时必须失败，不能修改排序状态。
    /// </summary>
    [Fact]
    public void Lambda_WhenOrderByUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.OrderBy<Sample>(
            item => new object[] { item.IntValue }));
    }

    /// <summary>
    /// 测试目的：同类型多来源用于 Having 且未提供 alias 时必须失败，不能修改 Having 状态。
    /// </summary>
    [Fact]
    public void Lambda_WhenHavingUsesAmbiguousSourceWithoutAlias_ShouldKeepQueryStateUnchanged()
    {
        AssertAmbiguousSourceMutationFails(query => query.Having<Sample>(item => item.IntValue > 0));
    }

    /// <summary>
    /// 测试目的：最终非泛型 public API 的 1～10 个根来源必须按显式 alias 和参数顺序渲染完整 SQL。
    /// </summary>
    [Theory]
    [InlineData(1, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(2, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(3, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(4, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3], [Sample] As [r4] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(5, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3], [Sample] As [r4], [Sample] As [r5] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(6, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3], [Sample] As [r4], [Sample] As [r5], [Sample] As [r6] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(7, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3], [Sample] As [r4], [Sample] As [r5], [Sample] As [r6], [Sample] As [r7] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(8, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3], [Sample] As [r4], [Sample] As [r5], [Sample] As [r6], [Sample] As [r7], [Sample] As [r8] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(9, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3], [Sample] As [r4], [Sample] As [r5], [Sample] As [r6], [Sample] As [r7], [Sample] As [r8], [Sample] As [r9] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(10, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1], [Sample] As [r2], [Sample] As [r3], [Sample] As [r4], [Sample] As [r5], [Sample] As [r6], [Sample] As [r7], [Sample] As [r8], [Sample] As [r9], [Sample] As [r10] \r\nWhere [r1].[IntValue]=@_p_0")]
    public void Lambda_WhenOneThroughTenSourcesAreAdded_ShouldRenderCompleteSql(int count, string expectedSql)
    {
        // Arrange
        var query = CreateLambdaQuery().From<Sample>("r1");
        for (var index = 2; index <= count; index++)
            query.From<Sample>($"r{index}");
        query.Select<Sample>(item => new object[] { item.IntValue }, "r1")
            .Where<Sample, int>(item => item.IntValue, count, "r1");

        // Act
        var sql = query.ToSql();
        var parameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal(expectedSql, sql);
        Assert.Equal(new[] { "@_p_0" }, parameters.Keys.ToArray());
        Assert.Equal(new object[] { count }, parameters.Values.ToArray());
    }

    /// <summary>
    /// 测试目的：最终非泛型 public API 的 2～10 个连续 Join 必须按显式左右 alias 渲染完整 SQL 和参数快照。
    /// </summary>
    [Theory]
    [InlineData(2, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(3, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(4, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nJoin [Sample] As [r4] On [r3].[IntValue]=[r4].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(5, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nJoin [Sample] As [r4] On [r3].[IntValue]=[r4].[IntValue] \r\nJoin [Sample] As [r5] On [r4].[IntValue]=[r5].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(6, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nJoin [Sample] As [r4] On [r3].[IntValue]=[r4].[IntValue] \r\nJoin [Sample] As [r5] On [r4].[IntValue]=[r5].[IntValue] \r\nJoin [Sample] As [r6] On [r5].[IntValue]=[r6].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(7, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nJoin [Sample] As [r4] On [r3].[IntValue]=[r4].[IntValue] \r\nJoin [Sample] As [r5] On [r4].[IntValue]=[r5].[IntValue] \r\nJoin [Sample] As [r6] On [r5].[IntValue]=[r6].[IntValue] \r\nJoin [Sample] As [r7] On [r6].[IntValue]=[r7].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(8, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nJoin [Sample] As [r4] On [r3].[IntValue]=[r4].[IntValue] \r\nJoin [Sample] As [r5] On [r4].[IntValue]=[r5].[IntValue] \r\nJoin [Sample] As [r6] On [r5].[IntValue]=[r6].[IntValue] \r\nJoin [Sample] As [r7] On [r6].[IntValue]=[r7].[IntValue] \r\nJoin [Sample] As [r8] On [r7].[IntValue]=[r8].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(9, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nJoin [Sample] As [r4] On [r3].[IntValue]=[r4].[IntValue] \r\nJoin [Sample] As [r5] On [r4].[IntValue]=[r5].[IntValue] \r\nJoin [Sample] As [r6] On [r5].[IntValue]=[r6].[IntValue] \r\nJoin [Sample] As [r7] On [r6].[IntValue]=[r7].[IntValue] \r\nJoin [Sample] As [r8] On [r7].[IntValue]=[r8].[IntValue] \r\nJoin [Sample] As [r9] On [r8].[IntValue]=[r9].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    [InlineData(10, "Select [r1].[IntValue] \r\nFrom [Sample] As [r1] \r\nJoin [Sample] As [r2] On [r1].[IntValue]=[r2].[IntValue] \r\nJoin [Sample] As [r3] On [r2].[IntValue]=[r3].[IntValue] \r\nJoin [Sample] As [r4] On [r3].[IntValue]=[r4].[IntValue] \r\nJoin [Sample] As [r5] On [r4].[IntValue]=[r5].[IntValue] \r\nJoin [Sample] As [r6] On [r5].[IntValue]=[r6].[IntValue] \r\nJoin [Sample] As [r7] On [r6].[IntValue]=[r7].[IntValue] \r\nJoin [Sample] As [r8] On [r7].[IntValue]=[r8].[IntValue] \r\nJoin [Sample] As [r9] On [r8].[IntValue]=[r9].[IntValue] \r\nJoin [Sample] As [r10] On [r9].[IntValue]=[r10].[IntValue] \r\nWhere [r1].[IntValue]=@_p_0")]
    public void Lambda_WhenTwoThroughTenSourcesAreJoined_ShouldRenderCompleteSql(int count, string expectedSql)
    {
        // Arrange
        var query = CreateLambdaQuery().From<Sample>("r1");
        for (var index = 2; index <= count; index++)
        {
            query.Join<Sample, Sample>((left, right) => left.IntValue == right.IntValue,
                new SqlJoinOptions { RightAlias = $"r{index}", LeftAlias = $"r{index - 1}" });
        }
        query.Select<Sample>(item => new object[] { item.IntValue }, "r1")
            .Where<Sample, int>(item => item.IntValue, count, "r1");

        // Act
        var sql = query.ToSql();
        var parameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal(expectedSql, sql);
        Assert.Equal(new[] { "@_p_0" }, parameters.Keys.ToArray());
        Assert.Equal(new object[] { count }, parameters.Values.ToArray());
    }

    /// <summary>
    /// 测试目的：类型化 Join 的 SqlJoinOptions.Schema 必须影响右侧表引用，并保持完整 SQL 与 On 来源 alias 一致。
    /// </summary>
    [Fact]
    public void Lambda_WhenJoinOptionsSpecifySchema_ShouldRenderCompleteSql()
    {
        // Arrange
        var query = CreateLambdaQuery()
            .From<Sample>("left")
            .Join<Sample, Sample>((first, second) => first.IntValue == second.IntValue,
                new SqlJoinOptions
                {
                    RightAlias = "right",
                    LeftAlias = "left",
                    Schema = "reporting"
                })
            .Select<Sample>(item => new object[] { item.IntValue }, "left");

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Equal(
            "Select [left].[IntValue] \r\nFrom [Sample] As [left] \r\nJoin [reporting].[Sample] As [right] On [left].[IntValue]=[right].[IntValue]",
            sql);
    }

    /// <summary>
    /// 测试目的：SplitOn 虽不改变 SQL 文本，但会改变执行计划语义，修改后必须失效实例缓存并作用于后续计划。
    /// </summary>
    [Fact]
    public void SplitOn_WhenChanged_ShouldInvalidateCacheAndUpdateExecutionPlan()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var executor = new Mock<ISqlQueryPlanExecutor>();
        SqlQueryPlan capturedPlan = null;
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) =>
            {
                capturedPlan = plan;
                return new List<Sample>();
            });
        builder.From<Sample>("s").Select<Sample>(item => new object[] { item.IntValue });
        var query = new SqlQuery(executor.Object, builder);

        // Act
        var firstSql = query.ToSql();
        query.SplitOn("ReviewId");
        var secondSql = query.ToSql();
        query.ToList<Sample>();

        // Assert
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
        Assert.NotNull(capturedPlan);
        Assert.Equal("ReviewId", capturedPlan.SplitOn);
    }

    /// <summary>
    /// 测试目的：动态软删除过滤状态变化后不得命中上一次环境的 SQL 缓存。
    /// </summary>
    [Fact]
    public void ToSql_WhenDataFilterStateChanges_ShouldRenderCurrentEnvironment()
    {
        // Arrange
        var dataFilter = new DataFilter();
        var builder = new TestSqlBuilder(new SqlBuilderServices(dataFilter: dataFilter), TestDialect.Instance);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample5>("s")
            .Select<Sample5>(item => new object[] { item.StringValue });

        // Act
        string disabledSql;
        using (dataFilter.Disable<ISoftDelete>())
            disabledSql = query.ToSql();
        var enabledSql = query.ToSql();

        // Assert
        Assert.Equal("Select [s].[StringValue] \r\nFrom [Sample5] As [s]", disabledSql);
        Assert.Equal("Select [s].[StringValue] \r\nFrom [Sample5] As [s] \r\nWhere [s].[IsDeleted]=@_p_0",
            enabledSql);
    }

    /// <summary>
    /// 测试目的：参数形状变化必须重新渲染，IN 长度和 null 条件必须分别生成完整 SQL 与参数快照。
    /// </summary>
    [Fact]
    public void ToSql_WhenParameterShapeChanges_ShouldPreserveSqlAndParameterSnapshot()
    {
        // Arrange
        var inQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>())
            .Where<Sample, object>(item => item.StringValue, new object[] { "a", "b" }, Operator.In);
        var nullQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>())
            .Where<Sample, string>(item => item.StringValue, null);

        // Act
        var inSql = inQuery.ToSql();
        var nullSql = nullQuery.ToSql();
        var inParameters = ((ISqlCommonPartAccessor)inQuery.GetBuilder()).ParameterManager.GetParams();
        var nullParameters = ((ISqlCommonPartAccessor)nullQuery.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[StringValue] In (@_p_0,@_p_1)",
            inSql);
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[StringValue] Is Null",
            nullSql);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, inParameters.Keys);
        Assert.Equal(new object[] { "a", "b" }, inParameters.Values);
        Assert.Empty(nullParameters);
    }

    /// <summary>
    /// 测试目的：不同规模的 IN 参数展开必须保持参数名称唯一、数量准确且 SQL 可渲染。
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Where_WhenInParameterSetScales_ShouldKeepUniqueParameterSnapshot(int count)
    {
        // Arrange
        var values = Enumerable.Range(0, count).Cast<object>().ToArray();
        var query = CreateQuery(new Mock<ISqlQueryPlanExecutor>())
            .Where<Sample, object>(item => item.IntValue, values, Operator.In);

        // Act
        var sql = query.ToSql();
        var parameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal(count, parameters.Count);
        Assert.Equal(count, parameters.Keys.Distinct(StringComparer.Ordinal).Count());
        var parameterTokens = string.Join(",", Enumerable.Range(0, count).Select(index => $"@_p_{index}"));
        Assert.Equal($"Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue] In ({parameterTokens})", sql);
    }

    /// <summary>
    /// 测试目的：WhereIf(false) 不得改变查询形状，WhereIf(true) 只在成功提交后追加一次条件。
    /// </summary>
    [Fact]
    public void ToSql_WhenWhereIfChanges_ShouldTouchOnlySuccessfulMutation()
    {
        // Arrange
        var falseQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>());
        var trueQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>());

        // Act
        falseQuery.WhereIf(false, (Sample item) => item.IntValue == 7);
        trueQuery.WhereIf(true, (Sample item) => item.IntValue == 7);

        // Assert
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s]", falseQuery.ToSql());
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue]=@_p_0",
            trueQuery.ToSql());
    }

    /// <summary>
    /// 测试目的：参数型 WhereIf(false) 不得写入参数或改变查询形状。
    /// </summary>
    [Fact]
    public void WhereIf_WhenParameterConditionIsFalse_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var query = CreateQuery(new Mock<ISqlQueryPlanExecutor>());
        var beforeSql = query.ToSql();

        // Act
        query.WhereIf(false, (Sample item) => item.StringValue, "ignored");
        var afterSql = query.ToSql();
        var parameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal(beforeSql, afterSql);
        Assert.Empty(parameters);
    }

    /// <summary>
    /// 测试目的：参数数量上限导致 Where 失败时，不得遗留参数、条件或已失效的缓存版本。
    /// </summary>
    [Fact]
    public void Where_WhenParameterLimitIsExceeded_ShouldKeepQueryStateUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 0, "test");
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new TestSqlBuilder(parameterManager: parameterManager))
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });
        var beforeSql = query.ToSql();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.Where<Sample, string>(
            item => item.StringValue, "blocked"));
        var afterSql = query.ToSql();
        var parameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Contains("参数数量", exception.Message, StringComparison.Ordinal);
        Assert.Equal(beforeSql, afterSql);
        Assert.Empty(parameters);
    }

    /// <summary>
    /// 测试目的：失败 Join 候选不得污染 SQL、参数或缓存状态，失败后合法查询仍可继续渲染。
    /// </summary>
    [Fact]
    public void ToSql_WhenJoinCandidateFails_ShouldKeepCachedShapeUnchanged()
    {
        // Arrange
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new CountingTestSqlBuilder())
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });
        var beforeSql = query.ToSql();
        var beforeParameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Act
        Assert.Throws<InvalidOperationException>(() => query.Join<Sample, Sample>(
            (left, right) => left.IntValue == right.IntValue,
            new SqlJoinOptions { RightAlias = "r", LeftAlias = "missing" }));
        var afterSql = query.ToSql();
        var afterParameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(beforeParameters, afterParameters);
    }

    /// <summary>
    /// 测试目的：实例缓存只能保存 SQL 形状数据，不得持有 Builder、连接、事务、Scope 或失效的参数布局缓存。
    /// </summary>
    [Fact]
    public void QueryInstanceCache_ShouldNotHoldExecutionResourcesOrParameterLayout()
    {
        // Arrange
        var fields = typeof(SqlQuery).GetFields(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        // Act
        var cachedFields = fields.Where(field => field.Name.Contains("cached", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        Assert.Contains(cachedFields, field => field.Name == "_cachedSql" && field.FieldType == typeof(string));
        Assert.DoesNotContain(fields, field => field.Name == "_cachedParameterLayout");
        Assert.DoesNotContain(cachedFields, field => field.FieldType != typeof(string) && field.FieldType != typeof(long));
        Assert.DoesNotContain(cachedFields, field => field.FieldType.Name.Contains("Builder", StringComparison.Ordinal));
        Assert.DoesNotContain(cachedFields, field => field.FieldType.Name.Contains("Connection", StringComparison.Ordinal));
        Assert.DoesNotContain(cachedFields, field => field.FieldType.Name.Contains("Transaction", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：Raw 文本描述不得复用结构化查询实例的 SQL 缓存，且公开参数读取必须返回独立快照。
    /// </summary>
    [Fact]
    public void RawQueryInstance_ShouldNotShareStructuredSqlCache()
    {
        // Arrange
        var raw = new SqlTextQuery(new Mock<ISqlQueryPlanExecutor>().Object, "Select @value",
            new Dictionary<string, object> { ["value"] = "first" });

        // Act
        var firstParameters = Assert.IsType<Dictionary<string, object>>(raw.Parameters);
        firstParameters["value"] = "changed";
        var secondParameters = Assert.IsType<Dictionary<string, object>>(raw.Parameters);

        // Assert
        Assert.Null(typeof(SqlTextQuery).GetField("_cachedSql",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        Assert.NotSame(firstParameters, secondParameters);
        Assert.Equal("first", secondParameters["value"]);
    }

    /// <summary>
    /// 测试目的：不同 Provider 方言、映射配置和租户过滤环境创建的查询实例不得互用 SQL 或参数缓存。
    /// </summary>
    [Fact]
    public void QueryInstanceCache_WhenEnvironmentDiffers_ShouldKeepSqlAndParametersIsolated()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var metadata = new SqlMetadataOptions();
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "default",
            MappingProfile = "read",
            TableName = "users_default",
            Columns =
            {
                [nameof(Sample.StringValue)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(Sample.StringValue),
                    ColumnName = "status_default"
                }
            }
        });
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "reporting",
            MappingProfile = "write",
            TableName = "users_reporting",
            Columns =
            {
                [nameof(Sample.StringValue)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(Sample.StringValue),
                    ColumnName = "status_reporting"
                }
            }
        });

        var defaultQuery = CreateEnvironmentQuery(executor.Object, metadata, TestDialect.Instance, "default",
            "read", "tenant-a");
        var reportingQuery = CreateEnvironmentQuery(executor.Object, metadata, TestDialect.Instance, "reporting",
            "write", "tenant-b");
        var alternateDialectQuery = CreateEnvironmentQuery(executor.Object, metadata, TestDialect2.Instance, "default",
            "read", "tenant-a");

        // Act
        var defaultSql = defaultQuery.ToSql();
        var reportingSql = reportingQuery.ToSql();
        var alternateDialectSql = alternateDialectQuery.ToSql();

        // Assert
        Assert.Equal("Select [s].[status_default] \r\nFrom [as_Sample].[users_default] As [s] \r\nWhere [s].[status_default]=@_p_0",
            defaultSql);
        Assert.Equal("Select [s].[status_reporting] \r\nFrom [as_Sample].[users_reporting] As [s] \r\nWhere [s].[status_reporting]=@_p_0",
            reportingSql);
        Assert.Equal("Select $$s&&&.$$status_default&&& \r\nFrom $as_Sample&.$users_default& As $s& \r\nWhere $$s&&&.$$status_default&&&=*_p_0",
            alternateDialectSql);
        Assert.NotEqual(defaultSql, reportingSql);
        Assert.NotEqual(defaultSql, alternateDialectSql);
        Assert.Equal("tenant-a", GetParameterValue(defaultQuery));
        Assert.Equal("tenant-b", GetParameterValue(reportingQuery));
        Assert.Equal("tenant-a", GetParameterValue(alternateDialectQuery));
    }

    /// <summary>
    /// 测试目的：Frozen/Completed 查询克隆后应得到独立 Draft，并建立来源父上下文关系。
    /// </summary>
    [Fact]
    public void Clone_WhenSourceIsCompleted_ShouldCreateIndependentDraftWithParentContext()
    {
        // Arrange
        var plans = new List<SqlQueryPlan>();
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) =>
            {
                plans.Add(plan);
                return Execute(plan, () => new List<Sample>());
            });
        var source = CreateQuery(executor);

        // Act
        source.ToList<Sample>();
        var clone = source.Clone();
        clone.Where<Sample>(item => item.IntValue == 7);
        clone.ToList<Sample>();

        // Assert
        Assert.Equal(2, plans.Count);
        Assert.NotEqual(plans[0].QueryContextId, plans[1].QueryContextId);
        Assert.Equal(plans[0].QueryContextId, plans[1].ParentQueryContextId);
        Assert.Throws<InvalidOperationException>(() => source.Where<Sample>(item => item.IntValue == 8));
        Assert.Contains("@_p_0", clone.ToSql());
    }

    /// <summary>
    /// 测试目的：来源和 Clone 均处于 Draft 时，双向追加条件必须保持 SQL 与参数状态独立。
    /// </summary>
    [Fact]
    public void Clone_WhenSourceAndCloneAreMutated_ShouldKeepBothStatesIndependent()
    {
        // Arrange
        var source = CreateQuery(new Mock<ISqlQueryPlanExecutor>());
        var clone = source.Clone();

        // Act
        source.Where<Sample>(item => item.IntValue == 7);
        clone.Where<Sample>(item => item.IntValue == 8);

        // Assert
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue]=@_p_0",
            source.ToSql());
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue]=@_p_0",
            clone.ToSql());
        Assert.Equal(7, GetSingleParameterValue(source));
        Assert.Equal(8, GetSingleParameterValue(clone));
    }

    /// <summary>
    /// 测试目的：同一查询模板的独立 Clone 并发执行时，参数快照和执行上下文不得相互覆盖。
    /// </summary>
    [Fact]
    public async Task Clone_WhenExecutedConcurrently_ShouldKeepParameterSnapshotsIsolated()
    {
        // Arrange
        var values = new System.Collections.Concurrent.ConcurrentBag<int>();
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) =>
            {
                var parameters = ((ISqlCommonPartAccessor)plan.GetBuilder()).ParameterManager.GetParams();
                values.Add(Assert.IsType<int>(parameters.Values.Single()));
                return new List<Sample>();
            });
        var source = CreateQuery(executor);
        var first = source.Clone().Where<Sample>(item => item.IntValue == 7);
        var second = source.Clone().Where<Sample>(item => item.IntValue == 8);

        // Act
        await Task.WhenAll(
            Task.Run(() => first.ToList<Sample>()),
            Task.Run(() => second.ToList<Sample>()));

        // Assert
        Assert.Equal(new[] { 7, 8 }, values.OrderBy(item => item).ToArray());
    }

    /// <summary>
    /// 测试目的：首次终结执行应冻结查询描述，之后继续修改必须立即拒绝。
    /// </summary>
    [Fact]
    public void Terminal_WhenQueryIsExecuted_ShouldFreezeDescription()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () => new List<Sample>()));
        var query = CreateQuery(executor);

        // Act
        query.ToList<Sample>();
        var exception = Assert.Throws<InvalidOperationException>(() => query.Where<Sample>(item => item.IntValue == 7));

        // Assert
        Assert.Equal("查询已冻结，不能继续修改查询描述。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Frozen/Completed 查询应允许重复执行，每次执行都必须获得独立执行回调。
    /// </summary>
    [Fact]
    public void Terminal_WhenQueryIsExecutedRepeatedly_ShouldReuseFrozenDescription()
    {
        // Arrange
        var executionCount = 0;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () =>
            {
                executionCount++;
                return new List<Sample>();
            }));
        var query = CreateQuery(executor);

        // Act
        var first = query.ToList<Sample>();
        var second = query.ToList<Sample>();

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(2, executionCount);
    }

    /// <summary>
    /// 测试目的：同一查询执行期间重入另一个终结入口必须拒绝，并在外层执行结束后恢复可用。
    /// </summary>
    [Fact]
    public void Terminal_WhenExecutionIsActive_ShouldRejectReentrantExecutionAndReleaseLease()
    {
        // Arrange
        var reentrantException = default(InvalidOperationException);
        var reentered = false;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var query = CreateQuery(executor);
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () =>
            {
                if (!reentered)
                {
                    reentered = true;
                    reentrantException = Assert.Throws<InvalidOperationException>(() => query.ToList<Sample>());
                }

                return new List<Sample>();
            }));

        // Act
        query.ToList<Sample>();
        query.ToList<Sample>();

        // Assert
        Assert.NotNull(reentrantException);
        Assert.Equal("当前查询正在执行，不能并发执行同一查询描述。", reentrantException.Message);
        Assert.True(reentered);
    }

    /// <summary>
    /// 测试目的：执行失败时仍应完成查询生命周期并释放租约，避免后续执行永久被拒绝。
    /// </summary>
    [Fact]
    public void Terminal_WhenExecutionFails_ShouldReleaseLease()
    {
        // Arrange
        var shouldFail = true;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () =>
            {
                if (shouldFail)
                {
                    shouldFail = false;
                    throw new InvalidOperationException("受控查询执行异常。");
                }

                return new List<Sample>();
            }));
        var query = CreateQuery(executor);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.ToList<Sample>());
        var result = query.ToList<Sample>();

        // Assert
        Assert.Equal("受控查询执行异常。", exception.Message);
        Assert.Empty(result);
    }

    /// <summary>
    /// 测试目的：流式枚举未结束时应持有执行租约，枚举 Dispose 后必须释放租约并允许再次执行。
    /// </summary>
    [Fact]
    public void Streaming_WhenEnumeratorDisposed_ShouldReleaseLease()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.AsEnumerable<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Stream(plan));
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () => new List<Sample>()));
        var query = CreateQuery(executor);

        // Act
        using var enumerator = query.AsEnumerable<Sample>().GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var activeException = Assert.Throws<InvalidOperationException>(() => query.ToList<Sample>());
        enumerator.Dispose();
        var result = query.ToList<Sample>();

        // Assert
        Assert.Equal("当前查询正在执行，不能并发执行同一查询描述。", activeException.Message);
        Assert.Empty(result);
    }

    /// <summary>
    /// 创建带有确定投影的非泛型 Lambda 查询描述。
    /// </summary>
    /// <param name="executor">受控计划执行器。</param>
    /// <returns>待测试查询描述。</returns>
    private static SqlLambdaQuery CreateQuery(Mock<ISqlQueryPlanExecutor> executor) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });

    private static SqlLambdaQuery CreateLambdaQuery() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder());

    private static void AssertAmbiguousSourceMutationFails(Func<SqlLambdaQuery, SqlLambdaQuery> mutation)
    {
        var query = CreateLambdaQuery()
            .From<Sample>("left")
            .From<Sample>("right")
            .Select<Sample>(item => new object[] { item.IntValue }, "left");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetShapeVersion(query);

        var exception = Assert.Throws<InvalidOperationException>(() => mutation(query));

        Assert.Contains("查询来源不唯一", exception.Message, StringComparison.Ordinal);
        Assert.Equal("Select [left].[IntValue] \r\nFrom [Sample] As [left], [Sample] As [right]", beforeSql);
        Assert.Equal(beforeSql, query.ToSql());
        Assert.Equal(beforeShapeVersion, GetShapeVersion(query));
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
    }

    private static void AssertDuplicateExplicitAliasBindingFails(Action<SqlLambdaQuery> mutation)
    {
        var query = CreateLambdaQuery()
            .From<Sample>("left")
            .From<Sample>("right")
            .Select<Sample>(item => new object[] { item.IntValue }, "left");
        var beforeSql = query.ToSql();
        var beforeShapeVersion = GetShapeVersion(query);
        var beforeCachedSql = GetCachedSql(query);
        var beforeCachedVersion = GetCachedVersion(query);

        var exception = Assert.Throws<InvalidOperationException>(() => mutation(query));

        Assert.Contains("同一查询来源", exception.Message, StringComparison.Ordinal);
        Assert.Equal(beforeSql, query.ToSql());
        Assert.Equal(beforeShapeVersion, GetShapeVersion(query));
        Assert.Equal(beforeCachedSql, GetCachedSql(query));
        Assert.Equal(beforeCachedVersion, GetCachedVersion(query));
        Assert.Empty(((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams());
    }

    private static long GetShapeVersion(SqlLambdaQuery query)
    {
        var coreField = typeof(SqlLambdaQuery).GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic);
        var core = coreField?.GetValue(query);
        var innerQueryField = core?.GetType().GetField("_query", BindingFlags.Instance | BindingFlags.NonPublic);
        var innerQuery = innerQueryField?.GetValue(core);
        var shapeVersionField = innerQuery?.GetType().GetField("_shapeVersion",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(shapeVersionField);
        return (long)shapeVersionField.GetValue(innerQuery);
    }

    private static long GetFluentShapeVersion(SqlFluentQuery query)
    {
        var innerQueryField = typeof(SqlFluentQuery).GetField("_query", BindingFlags.Instance | BindingFlags.NonPublic);
        var innerQuery = innerQueryField?.GetValue(query);
        var shapeVersionField = innerQuery?.GetType().GetField("_shapeVersion",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(shapeVersionField);
        return (long)shapeVersionField.GetValue(innerQuery);
    }

    private static T GetFluentQueryField<T>(SqlFluentQuery query, string fieldName)
    {
        var innerQueryField = typeof(SqlFluentQuery).GetField("_query", BindingFlags.Instance | BindingFlags.NonPublic);
        var innerQuery = innerQueryField?.GetValue(query);
        var field = innerQuery?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field.GetValue(innerQuery);
    }

    private static long GetCachedVersion(SqlLambdaQuery query) => GetQueryField<long>(query, "_cachedVersion");

    private static string GetCachedSql(SqlLambdaQuery query) => GetQueryField<string>(query, "_cachedSql");

    private static T GetQueryField<T>(SqlLambdaQuery query, string fieldName)
    {
        var coreField = typeof(SqlLambdaQuery).GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic);
        var core = coreField?.GetValue(query);
        var innerQueryField = core?.GetType().GetField("_query", BindingFlags.Instance | BindingFlags.NonPublic);
        var innerQuery = innerQueryField?.GetValue(core);
        var field = innerQuery?.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field.GetValue(innerQuery);
    }

    private static SqlLambdaQuery CreateEnvironmentQuery(ISqlQueryPlanExecutor executor,
        SqlMetadataOptions metadata, IDialect dialect, string dbKey, string mappingProfile, string tenantId)
    {
        var options = new SqlOptions().SetDatabaseContext(new DatabaseContext
        {
            DbKey = dbKey,
            MappingProfile = mappingProfile,
            TenantId = tenantId,
            DataSource = new SqlDataSourceDescriptor
            {
                Key = dbKey,
                DatabaseType = DatabaseType.SqlServer,
                ConnectionString = $"Server={dbKey};"
            }
        });
        var services = new SqlBuilderServices(metadataOptions: metadata, options: options,
            entityModelMetadataProvider: new TestEntityMetadata(), filters: new ISqlFilter[] { new TenantFilter() });
        var builder = new TestSqlBuilder(services, dialect);
        return SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.StringValue });
    }

    private sealed class SampleProjection
    {
        public int IntValue { get; set; }
    }

    private static object GetParameterValue(SqlLambdaQuery query)
    {
        var snapshot = ((SqlBuilderBase)query.GetBuilder()).CreateExecutionBuilderSnapshot();
        _ = snapshot.ToSql();
        return ((ISqlCommonPartAccessor)snapshot).ParameterManager.GetParams().Values.Single();
    }

    private static object GetSingleParameterValue(SqlLambdaQuery query) =>
        ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams().Values.Single();

    /// <summary>
    /// 按真实执行器的生命周期回调顺序执行受控操作。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="plan">待执行计划。</param>
    /// <param name="operation">受控数据库操作。</param>
    /// <returns>受控操作结果。</returns>
    private static TResult Execute<TResult>(SqlQueryPlan plan, Func<TResult> operation)
    {
        var started = false;
        try
        {
            plan.NotifyExecutionStarted();
            started = true;
            return operation();
        }
        finally
        {
            if (started)
                plan.NotifyExecutionFinished();
        }
    }

    /// <summary>
    /// 创建持有租约直到枚举完成的同步结果流。
    /// </summary>
    /// <param name="plan">待执行计划。</param>
    /// <returns>受控结果流。</returns>
    private static IEnumerable<Sample> Stream(SqlQueryPlan plan)
    {
        var started = false;
        try
        {
            plan.NotifyExecutionStarted();
            started = true;
            yield return new Sample { IntValue = 1 };
        }
        finally
        {
            if (started)
                plan.NotifyExecutionFinished();
        }
    }

    /// <summary>
    /// 统计 SQL 渲染次数的测试 Builder。
    /// </summary>
    private sealed class CountingTestSqlBuilder : TestSqlBuilder
    {
        public CountingTestSqlBuilder() : this(new RenderCounters(), null)
        {
        }

        public CountingTestSqlBuilder(IParameterManager parameterManager) : this(new RenderCounters(), parameterManager)
        {
        }

        private CountingTestSqlBuilder(RenderCounters counters) : this(counters, null)
        {
        }

        private CountingTestSqlBuilder(RenderCounters counters, IParameterManager parameterManager)
            : base(parameterManager: parameterManager) => Counters = counters;

        public RenderCounters Counters { get; }

        public override string ToSql()
        {
            Counters.ToSqlCallCount++;
            return base.ToSql();
        }

        public override ISqlBuilder Clone()
        {
            var builder = new CountingTestSqlBuilder(Counters);
            builder.Clone(this);
            return builder;
        }

        public sealed class RenderCounters
        {
            public int ToSqlCallCount { get; set; }
        }
    }

    private sealed class ThrowingCloneBuilder : TestSqlBuilder
    {
        private readonly string _message;

        public ThrowingCloneBuilder(string message) => _message = message;

        public override ISqlBuilder Clone()
        {
            throw new InvalidOperationException(_message);
        }
    }

    private sealed class TenantFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context)
        {
            foreach (var source in context.Sources.Where(item => item.EntityType == typeof(Sample)))
                context.AddPredicate(source, context.GetColumn(source, nameof(Sample.StringValue)),
                    context.DatabaseContext?.TenantId);
        }
    }
}