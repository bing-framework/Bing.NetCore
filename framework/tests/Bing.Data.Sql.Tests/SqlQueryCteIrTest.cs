using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>CTE 前缀与主 Select 的冻结边界，不重新复制查询草稿。</summary>
public sealed class SqlQueryCteIrTest
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var state in new[] { "unordered", "ordered", "paged" })
        foreach (var entry in new[] { "freeze", "default", "cached" })
            yield return new object[] { shape, state, entry };
    }

    /// <summary>默认、缓存与显式冻结入口统一派生，CTE 参数重名只在首次渲染时解决。</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Cte_CountAndPage_ShouldUseFrozenStructure(string shape, string state, string entry)
    {
        var calls = new Calls();
        var builder = Create(shape, state, calls);
        SqlQueryPlan source;
        if (entry == "freeze") source = SqlQueryPlan.Create(builder, "split").Freeze();
        else
        {
            var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
            if (entry == "cached") query.ToSql();
            source = query.GetPlan();
        }
        source.SetContext("cte-111", "parent-111", "Data");
        var originalSql = source.CommandText;
        var originalValues = Values(source);
        calls.Clones = calls.Renders = 0;

        Assert.Null(source.PlanningBuilder);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        var page = SqlBuilderRuntimeBridge.CreatePageExecutionPlan(source,
            new Pager(3, 4, "Name Desc,Id Desc"), new RejectBuilderSource());
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2, "Id,Name"));
        var repeatedCount = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);

        var select = Select(shape);
        var prefix = state == "paged" ? Prefix.Replace("@_p_1", "@_p_3") : Prefix;
        var offset = state == "paged" ? 1 : 2;
        var limit = state == "paged" ? 2 : 3;
        var group = shape == "group" ? " \r\nGroup By [Name]" : "";
        var order = state == "unordered" ? "[Name] Desc,[Id] Desc" : "[Name],[Id] Desc";
        var countSql = prefix + (shape == "simple"
            ? "Select Count(*) \r\n" + Body
            : $"Select Count(*) \r\nFrom ({select} \r\n{Body}{group}) As [t]");
        var pageSql = $"{prefix}{select} \r\n{Body}{group} \r\nOrder By {order} \r\nOffset @_p_{offset} Rows Fetch Next @_p_{limit} Rows Only";
        Assert.Equal(countSql, count.CommandText);
        Assert.Equal(countSql, repeatedCount.CommandText);
        Assert.Equal(pageSql, page.CommandText);
        Assert.Equal(pageSql, repeated.CommandText);
        Assert.Equal(new object[] { 9, 7 }, Values(count));
        Assert.Equal(new object[] { 9, 7 }, Values(repeatedCount));
        Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2", "@_p_3" },
            Assert.IsType<SqlParam[]>(page.Parameters).Select(p => p.Name));
        Assert.Equal(state == "paged" ? new object[] { 9, 8, 4, 7 } : new object[] { 9, 7, 8, 4 }, Values(page));
        Assert.Equal(state == "paged" ? new object[] { 9, 2, 2, 7 } : new object[] { 9, 7, 2, 2 }, Values(repeated));
        Assert.Equal(originalSql, source.CommandText);
        Assert.Equal(originalValues, Values(source));
        Assert.Equal("cte-111", count.QueryContextId);
        Assert.Equal("parent-111", page.ParentQueryContextId);
        Assert.Equal("Count", count.Phase);
        Assert.Equal("Data", page.Phase);
        Assert.Equal(source.SplitOn, page.SplitOn);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>直接 Builder 计划同样保留 CTE 前缀，只包装分组、去重或聚合的主查询。</summary>
    [Theory]
    [InlineData("simple")]
    [InlineData("group")]
    [InlineData("distinct")]
    [InlineData("aggregate")]
    public void BuilderCount_ShouldShareCteCountSemantics(string shape)
    {
        var builder = Create(shape, "paged", new Calls());
        var originalSql = builder.ToSql();
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(SqlQueryPlan.Create(builder));
        var group = shape == "group" ? " \r\nGroup By [Name]" : "";
        Assert.Equal(Prefix.Replace("@_p_1", "@_p_3") + (shape == "simple" ? "Select Count(*) \r\n" + Body :
            $"Select Count(*) \r\nFrom ({Select(shape)} \r\n{Body}{group}) As [t]"), count.CommandText);
        Assert.Equal(new object[] { 9, 7 }, Values(count));
        Assert.Equal(originalSql, builder.ToSql());
    }

    /// <summary>CTE 内部排序和分页不属于外层 Count/Page 的替换范围。</summary>
    [Fact]
    public void CtePage_ShouldKeepInnerPageAndSourceIsolation()
    {
        var nested = new TestSqlBuilder().Select("Name").From("archive").Where("Status", 7)
            .OrderBy("Name").Skip(1).Take(3);
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("recent")
            .With("recent", nested).Where("TenantId", 9).OrderBy("Name").Skip(5).Take(2)).Freeze();
        nested.Where("Changed", 99);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Id Desc"));
        const string prefix = "With [recent] \r\nAs (Select [Name] \r\nFrom [archive] \r\nWhere [Status]=@_p_3 \r\nOrder By [Name] \r\nOffset @_p_4 Rows Fetch Next @_p_5 Rows Only)\r\n";
        Assert.Equal(prefix + "Select [Name] \r\n" + Body + " \r\nOrder By [Name],[Id] Desc \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        Assert.Equal(prefix + "Select Count(*) \r\n" + Body, count.CommandText);
        Assert.Equal(new object[] { 9, 7, 1, 3 }, Values(count));
        Assert.Equal(new object[] { 9, 8, 4, 7, 1, 3 }, Values(page));
        Assert.Equal(new object[] { 9, 5, 2, 7, 1, 3 }, Values(source));
    }

    /// <summary>多个 CTE 的声明顺序及内部结构保留，派生计划不共享请求参数。</summary>
    [Fact]
    public async Task MultipleCtes_ConcurrentDerivation_ShouldKeepPrefixAndParameters()
    {
        var first = new TestSqlBuilder().Select("Name").From("archive").Where("Status", 7);
        var second = new TestSqlBuilder().Select("Name").From("recent").Where("Kind", 8);
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("filtered")
            .With("recent", first).With("filtered", second).Distinct()).Freeze();
        const string prefix = "With [recent] \r\nAs (Select [Name] \r\nFrom [archive] \r\nWhere [Status]=@_p_0),\r\n[filtered] \r\nAs (Select [Name] \r\nFrom [recent] \r\nWhere [Kind]=@_p_1)\r\n";
        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(number, 3, "Name"));
            Assert.Equal(prefix + "Select Distinct [Name] \r\nFrom [filtered] \r\nOrder By [Name] \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only", page.CommandText);
            Assert.Equal(new object[] { 7, 8, (number - 1) * 3, 3 }, Values(page));
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
            Assert.Equal(prefix + "Select Count(*) \r\nFrom (Select Distinct [Name] \r\nFrom [filtered]) As [t]", count.CommandText);
            Assert.Equal(new object[] { 7, 8 }, Values(count));
            Assert.IsType<SqlParam[]>(page.Parameters)[0] = new SqlParam("@changed", 99);
        })));
        Assert.Equal(prefix + "Select Distinct [Name] \r\nFrom [filtered]", source.CommandText);
        Assert.Equal(new object[] { 7, 8 }, Values(source));
        Assert.Null(source.PlanningBuilder);
    }

    /// <summary>完整支持排序和分页的普通计划不再保留额外的规划 Builder。</summary>
    [Theory]
    [InlineData("simple")]
    [InlineData("group")]
    [InlineData("distinct")]
    [InlineData("aggregate")]
    public void FullyStructuredFreeze_ShouldReleasePlanningSource(string shape)
    {
        var builder = new TestSqlBuilder();
        if (shape == "aggregate") builder.CountAll("Total");
        else builder.Select("Name");
        builder.From("samples");
        if (shape == "group") builder.GroupBy("Name");
        if (shape == "distinct") builder.Distinct();
        var source = SqlQueryPlan.Create(builder).Freeze();
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(Select(shape) + " \r\nFrom [samples]" +
            (shape == "group" ? " \r\nGroup By [Name]" : ""), source.CommandText);
    }

    private const string Prefix = "With [recent] \r\nAs (Select [Name] \r\nFrom [archive] \r\nWhere [Status]=@_p_1)\r\n";
    private const string Body = "From [recent] \r\nWhere [TenantId]=@_p_0";
    private static string Select(string shape) => shape == "aggregate" ? "Select Count(*) As [Total]" :
        shape == "distinct" ? "Select Distinct [Name]" : "Select [Name]";
    private static CountedBuilder Create(string shape, string state, Calls calls)
    {
        var builder = new CountedBuilder(calls);
        if (shape == "aggregate") builder.CountAll("Total");
        else builder.Select("Name");
        builder.From("recent").Where("TenantId", 9).With("recent",
            new CountedBuilder(calls).Select("Name").From("archive").Where("Status", 7));
        if (shape == "group") builder.GroupBy("Name");
        if (shape == "distinct") builder.Distinct();
        if (state != "unordered") builder.OrderBy("Name");
        if (state == "paged") builder.Skip(5).Take(2);
        return builder;
    }
    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(p => p.Value).ToArray();
    private sealed class Calls { internal int Clones; internal int Renders; }
    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly Calls _calls;
        internal CountedBuilder(Calls calls, IParameterManager parameters = null) : base(parameterManager: parameters) => _calls = calls;
        public override ISqlBuilder Clone() { _calls.Clones++; return base.Clone(); }
        public override string ToSql() { _calls.Renders++; return base.ToSql(); }
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameters) => new CountedBuilder(_calls, parameters);
    }
    private sealed class RejectBuilderSource : ISqlQueryBuilderSource
    {
        public ISqlBuilder CreateIndependentSqlBuilder() => throw new InvalidOperationException("CTE IR 不应创建 Builder。");
    }
}
