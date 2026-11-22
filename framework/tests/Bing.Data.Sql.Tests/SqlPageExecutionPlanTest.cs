using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>分页执行只在原生 SQL 包装时向执行器请求独立 Builder。</summary>
public sealed class SqlPageExecutionPlanTest
{
    public static IEnumerable<object[]> StructuredCases()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var frozen in new[] { false, true })
        foreach (var newOrder in new[] { false, true })
            yield return new object[] { shape, frozen, newOrder };
    }

    /// <summary>草稿、冻结 IR 与新排序规划均使用计划自身的来源。</summary>
    [Theory]
    [MemberData(nameof(StructuredCases))]
    public void StructuredPage_ShouldNotRequestProviderBuilder(string shape, bool frozen, bool newOrder)
    {
        var builder = CreateBuilder(shape).OrderBy("Name").Skip(5).Take(2);
        var plan = SqlQueryPlan.Create(builder);
        if (frozen)
            plan = plan.Freeze();
        plan.SetContext("query", "parent");
        var original = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan);
        var source = new BuilderSource();

        var page = SqlBuilderRuntimeBridge.CreatePageExecutionPlan(plan, new Pager(3, 4, newOrder ? "Id" : "Name"), source);

        Assert.Equal(0, source.Calls);
        var select = shape == "aggregate" ? "Select Count(*) As [Total]" :
            shape == "distinct" ? "Select Distinct [Name]" : "Select [Name]";
        var group = shape == "group" ? " \r\nGroup By [Name]" : "";
        var order = newOrder ? "[Name],[Id]" : "[Name]";
        Assert.Equal($"{select} \r\nFrom [samples] \r\nWhere [Status]=@_p_0{group} \r\nOrder By {order} \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Parameters(page).Select(parameter => parameter.Value));
        Assert.Equal(original.Sql, SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan).Sql);
        Assert.Equal(new object[] { 7, 5, 2 }, original.Parameters.Select(parameter => parameter.Value));
        Assert.Equal("query", page.QueryContextId);
        Assert.Equal("parent", page.ParentQueryContextId);
        Assert.Equal("Data", page.Phase);
    }

    /// <summary>没有规划来源的纯 IR 页面也不创建执行器 Builder。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PureIrPage_ShouldNotRequestProviderBuilder(bool explicitOrder)
    {
        var frozen = SqlQueryPlan.Create(CreateBuilder("simple").OrderBy("Name").Skip(5).Take(2)).Freeze();
        var plan = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var source = new BuilderSource();

        var page = SqlBuilderRuntimeBridge.CreatePageExecutionPlan(plan,
            new Pager(3, 4, explicitOrder ? "Name" : null), source);

        Assert.Equal(0, source.Calls);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Parameters(page).Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 7, 5, 2 }, Parameters(plan).Select(parameter => parameter.Value));
    }

    /// <summary>尚未分页的冻结查询仍沿用隔离规划源，不创建闲置空 Builder。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenUnpagedQuery_ShouldUsePlanningSourceWithoutProviderBuilder(bool grouped)
    {
        var plan = SqlQueryPlan.Create(CreateBuilder(grouped ? "group" : "simple")).Freeze();
        var source = new BuilderSource();

        var page = SqlBuilderRuntimeBridge.CreatePageExecutionPlan(plan, new Pager(3, 4, "Name"), source);

        Assert.Equal(0, source.Calls);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0" +
            (grouped ? " \r\nGroup By [Name]" : "") +
            " \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Parameters(page).Select(parameter => parameter.Value));
        Assert.Equal(7, Assert.Single(Parameters(plan)).Value);
    }

    /// <summary>原生 SQL 请求一次当前 Provider 的 Builder，避开已有参数并保留来源。</summary>
    [Fact]
    public void RawPage_ShouldRequestProviderBuilderOnceAndAvoidParameterCollision()
    {
        const string sql = "Select Name From samples Where Status=@_p_0";
        var plan = SqlQueryPlan.Create(sql, new Dictionary<string, object> { ["@_p_0"] = 7 });
        plan.SetContext("raw-query", "parent");
        var source = new BuilderSource(new SqliteBuilder());

        var page = SqlBuilderRuntimeBridge.CreatePageExecutionPlan(plan, new Pager(3, 4, "Name"), source);

        Assert.Equal(1, source.Calls);
        Assert.Equal("Select * \r\nFrom (Select Name From samples Where Status=@_p_0) __bing_raw_page \r\nOrder By `Name` Limit @_p_2 OFFSET @_p_1", page.CommandText);
        var parameters = Assert.IsAssignableFrom<ISqlParameterMap>(page.Parameters);
        Assert.Equal(7, Assert.IsType<Dictionary<string, object>>(parameters.Source)["@_p_0"]);
        Assert.Equal(new[] { "@_p_1", "@_p_2" }, parameters.GetItems().Select(item => item.Name));
        Assert.Equal(new object[] { 8, 4 }, parameters.GetItems().Select(item => item.Value));
        Assert.Equal(sql, plan.CommandText);
        Assert.Equal("raw-query", page.QueryContextId);
        Assert.Equal("parent", page.ParentQueryContextId);
    }

    /// <summary>Provider 来源失败保持原异常，不重复创建或回退到其他 Provider。</summary>
    [Fact]
    public void RawPage_WhenBuilderSourceFails_ShouldPropagateOriginalException()
    {
        var source = new BuilderSource();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlBuilderRuntimeBridge.CreatePageExecutionPlan(SqlQueryPlan.Create("Select Name From samples", null),
                new Pager(1, 2, "Name"), source));

        Assert.Same(source.Failure, exception);
        Assert.Equal(1, source.Calls);
    }

    /// <summary>推迟 Builder 创建不绕过原生分页的排序校验。</summary>
    [Fact]
    public void RawPage_WithInvalidOrder_ShouldKeepValidation()
    {
        var source = new BuilderSource(new SqliteBuilder());

        var exception = Assert.Throws<ArgumentException>(() =>
            SqlBuilderRuntimeBridge.CreatePageExecutionPlan(SqlQueryPlan.Create("Select Name From samples", null),
                new Pager(1, 2, "Name; delete from samples"), source));

        Assert.Equal("order", exception.ParamName);
        Assert.Equal(1, source.Calls);
    }

    /// <summary>现有 Provider 能力档案仍在创建原生分页计划时生效。</summary>
    [Fact]
    public void RawPage_WithUnsupportedPagination_ShouldKeepProviderGate()
    {
        var source = new BuilderSource(new SqlServerBuilder());

        var exception = Assert.Throws<NotSupportedException>(() =>
            SqlBuilderRuntimeBridge.CreatePageExecutionPlan(SqlQueryPlan.Create("Select Name From samples", null),
                new Pager(1, 2, "Name"), source));

        Assert.Equal("当前 SQL Provider 不支持原生 SQL 自动分页。", exception.Message);
        Assert.Equal("Query:RawPagination", exception.Data["Bing.Data.Sql.Capability"]);
        Assert.Equal(1, source.Calls);
    }

    private static ISqlBuilder CreateBuilder(string shape)
    {
        ISqlBuilder builder = new TestSqlBuilder();
        if (shape == "aggregate")
            builder.CountAll("Total");
        else
            builder.Select("Name");
        if (shape == "group")
            builder.GroupBy("Name");
        if (shape == "distinct")
            builder.Distinct();
        return builder.From("samples").Where("Status", 7);
    }

    private static SqlParam[] Parameters(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters);

    private sealed class BuilderSource : ISqlQueryBuilderSource
    {
        private readonly ISqlBuilder _builder;
        internal int Calls { get; private set; }
        internal InvalidOperationException Failure { get; } = new("Unexpected Provider Builder creation.");

        internal BuilderSource(ISqlBuilder builder = null) => _builder = builder;

        public ISqlBuilder CreateIndependentSqlBuilder()
        {
            Calls++;
            return _builder ?? throw Failure;
        }
    }
}
