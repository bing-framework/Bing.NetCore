using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>集合查询冻结后只从文本边界派生，不再次访问查询草稿。</summary>
public sealed class SqlQuerySetIrTest
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var operation in new[] { "Union", "Union All", "Intersect", "Except" })
        foreach (var cte in new[] { false, true })
        foreach (var state in new[] { "unordered", "ordered", "paged" })
        foreach (var entry in new[] { "freeze", "default", "cached" })
            yield return new object[] { operation, cte, state, entry };
    }

    /// <summary>主集合被整体计数，CTE 前缀、分支参数和源状态保持隔离。</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Set_CountAndPage_ShouldUseFrozenStructure(string operation, bool cte, string state, string entry)
    {
        var calls = new Calls();
        var builder = new CountedBuilder(calls);
        builder.Select("Name").From(cte ? "selected" : "samples").Where("Status", 9);
        if (cte) builder.With("selected", new TestSqlBuilder().Select("Name").From("archive").Where("Kind", 7));
        Apply(builder, new TestSqlBuilder().Select("Name").From("other").Where("Status", 8), operation);
        if (state != "unordered") builder.OrderBy("Name");
        if (state == "paged") builder.Skip(5).Take(2);
        SqlQueryPlan source;
        if (entry == "freeze") source = SqlQueryPlan.Create(builder, "split").Freeze();
        else
        {
            var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
            if (entry == "cached") query.ToSql();
            source = query.GetPlan();
        }
        source.SetContext("set-112", "parent-112", "Data");
        var originalSql = source.CommandText;
        var originalValues = Values(source);
        calls.Clones = calls.Renders = 0;
        Assert.Null(source.PlanningBuilder);

        var page = SqlBuilderRuntimeBridge.CreatePageExecutionPlan(source, new Pager(3, 4, "Name Desc"), new RejectSource());
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        var branch = state == "paged" ? (cte ? 4 : 3) : (cte ? 2 : 1);
        var prefix = cte ? $"With [selected] \r\nAs (Select [Name] \r\nFrom [archive] \r\nWhere [Kind]=@_p_{branch - 1})\r\n" : "";
        var body = $"(Select [Name] \r\nFrom [{(cte ? "selected" : "samples")}] \r\nWhere [Status]=@_p_0 \r\n) \r\n{operation} \r\n(Select [Name] \r\nFrom [other] \r\nWhere [Status]=@_p_{branch} \r\n)";
        var offset = state == "paged" ? 1 : branch + 1;
        var limit = offset + 1;
        var expectedPage = prefix + body + " \r\nOrder By [Name]" + (state == "unordered" ? " Desc" : "") +
            $" \r\nOffset @_p_{offset} Rows Fetch Next @_p_{limit} Rows Only";
        Assert.Equal(expectedPage, page.CommandText);
        Assert.Equal(expectedPage, repeated.CommandText);
        Assert.Equal(prefix + $"Select Count(*) \r\nFrom ({body}) As [t]", count.CommandText);
        Assert.Equal(cte ? new object[] { 9, 7, 8 } : new object[] { 9, 8 }, Values(count));
        var expected = state == "paged" ? (cte ? new object[] { 9, 8, 4, 7, 8 } : new object[] { 9, 8, 4, 8 }) :
            (cte ? new object[] { 9, 7, 8, 8, 4 } : new object[] { 9, 8, 8, 4 });
        Assert.Equal(expected, Values(page));
        Assert.Equal("set-112", count.QueryContextId);
        Assert.Equal("parent-112", page.ParentQueryContextId);
        Assert.Equal("Count", count.Phase);
        Assert.Equal(source.SplitOn, page.SplitOn);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
        Assert.Equal(originalSql, source.CommandText);
        Assert.Equal(originalValues, Values(source));
    }

    /// <summary>未冻结入口与冻结入口生成相同完整 Count，包含 CTE 组合。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuilderCount_ShouldShareSetSemantics(bool cte)
    {
        var builder = new TestSqlBuilder().Select("Name").From("samples").Where("Status", 9);
        if (cte) builder.With("selected", new TestSqlBuilder().Select("Name").From("archive"));
        builder.UnionAll(new TestSqlBuilder().Select("Name").From("other").Where("Status", 8));
        var sql = builder.ToSql();
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(SqlQueryPlan.Create(builder));
        var prefix = cte ? "With [selected] \r\nAs (Select [Name] \r\nFrom [archive])\r\n" : "";
        Assert.Equal(prefix + "Select Count(*) \r\nFrom ((Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\n) \r\nUnion All \r\n(Select [Name] \r\nFrom [other] \r\nWhere [Status]=@_p_1 \r\n)) As [t]", count.CommandText);
        Assert.Equal(new object[] { 9, 8 }, Values(count));
        Assert.Equal(sql, builder.ToSql());
    }

    /// <summary>混合集合运算在多请求中派生独立参数，根排序不进入 Count。</summary>
    [Fact]
    public async Task MixedSet_ConcurrentPages_ShouldKeepFrozenParameters()
    {
        var root = new TestSqlBuilder().Select("Name").From("a").Where("Status", 9);
        root.UnionAll(new TestSqlBuilder().Select("Name").From("b").Where("Status", 8))
            .Except(new TestSqlBuilder().Select("Name").From("c").Where("Status", 7));
        var source = SqlQueryPlan.Create(root).Freeze();
        const string body = "(Select [Name] \r\nFrom [a] \r\nWhere [Status]=@_p_0 \r\n) \r\nUnion All \r\n(Select [Name] \r\nFrom [b] \r\nWhere [Status]=@_p_1 \r\n) \r\nExcept \r\n(Select [Name] \r\nFrom [c] \r\nWhere [Status]=@_p_2 \r\n)";
        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(number, 3, "Name"));
            Assert.Equal(body + " \r\nOrder By [Name] \r\nOffset @_p_3 Rows Fetch Next @_p_4 Rows Only", page.CommandText);
            Assert.Equal(new object[] { 9, 8, 7, (number - 1) * 3, 3 }, Values(page));
            Assert.Equal("Select Count(*) \r\nFrom (" + body + ") As [t]", SqlBuilderRuntimeBridge.CreateCountPlan(page).CommandText);
            Assert.IsType<SqlParam[]>(page.Parameters)[0] = new SqlParam("@changed", 99);
        })));
        Assert.Equal(body, source.CommandText);
        Assert.Equal(new object[] { 9, 8, 7 }, Values(source));
    }

    /// <summary>冻结分支自己的分页不属于根 Count/Page 的参数清理范围。</summary>
    [Fact]
    public void SetFragment_InnerPage_ShouldSurviveOuterCount()
    {
        var fragment = new SqlQueryFragment("Select [Name] From [b] Order By [Name] Offset @inner Rows Fetch Next @size Rows Only",
            new[] { new SqlParam("@inner", 1), new SqlParam("@size", 3) }, SqlQueryFragmentKind.Union);
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("a").Where("Status", 9)
            .UnionAll(fragment).OrderBy("Name").Skip(5).Take(2)).Freeze();
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        const string body = "(Select [Name] \r\nFrom [a] \r\nWhere [Status]=@_p_0 \r\n) \r\nUnion All \r\n(Select [Name] From [b] Order By [Name] Offset @inner Rows Fetch Next @size Rows Only \r\n)";
        Assert.Equal(body + " \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        Assert.Equal("Select Count(*) \r\nFrom (" + body + ") As [t]", count.CommandText);
        Assert.Equal(new object[] { 9, 1, 3 }, Values(count));
        Assert.Equal(new object[] { 9, 5, 2, 1, 3 }, Values(source));
    }

    internal static void Apply(ISqlBuilder root, ISqlBuilder branch, string operation)
    {
        switch (operation)
        {
            case "Union": root.Union(branch); break;
            case "Union All": root.UnionAll(branch); break;
            case "Intersect": root.Intersect(branch); break;
            case "Except": root.Except(branch); break;
        }
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
    private sealed class RejectSource : ISqlQueryBuilderSource
    {
        public ISqlBuilder CreateIndependentSqlBuilder() => throw new InvalidOperationException("集合 IR 不应创建 Builder。");
    }
}
