using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>仅有 Skip 的冻结计划复用偏移槽位，不再保留可变 Builder。</summary>
public sealed class SqlQuerySkipOnlyIrTest
{
    public static IEnumerable<object[]> Shapes()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var entry in new[] { "plan", "provider", "execution" })
            yield return new object[] { shape, entry };
    }

    /// <summary>三个分页入口均只使用 IR，重复分页与 Count 不克隆或重新渲染。</summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void FrozenSkip_ShouldDeriveWithoutBuilder(string shape, string entry)
    {
        var calls = new RenderCalls();
        var builder = new CountedBuilder(calls);
        if (shape == "aggregate") builder.CountAll("Total");
        else builder.Select("Name");
        if (shape == "distinct") builder.Distinct();
        if (shape == "group") builder.GroupBy("Name");
        builder.From("samples").Where("Status", 7).OrderBy("Name").Skip(5);
        var source = SqlQueryPlan.Create(builder).Freeze();
        Assert.Null(source.PlanningBuilder);
        calls.Clones = calls.Renders = 0;
        var pager = new Pager(3, 4);
        var page = entry switch
        {
            "provider" => SqlBuilderRuntimeBridge.CreatePagePlan(source, pager, new TestSqlBuilder()),
            "execution" => SqlBuilderRuntimeBridge.CreatePageExecutionPlan(source, pager, new RejectBuilderSource()),
            _ => SqlBuilderRuntimeBridge.CreatePagePlan(source, pager)
        };
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        var select = shape == "aggregate" ? "Select Count(*) As [Total]" :
            shape == "distinct" ? "Select Distinct [Name]" : "Select [Name]";
        var group = shape == "group" ? " \r\nGroup By [Name]" : "";
        const string body = "From [samples] \r\nWhere [Status]=@_p_0";
        var sourceSql = $"{select} \r\n{body}{group} \r\nOrder By [Name]";
        var pageSql = sourceSql + " \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only";
        var countSql = shape == "simple" ? $"Select Count(*) \r\n{body}" :
            $"Select Count(*) \r\nFrom ({select} \r\n{body}{group}) As [t]";
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, new[] { "@_p_0", "@_p_1" }, 7, 5);
        Assert.Equal(pageSql, page.CommandText);
        AssertParameters(page, new[] { "@_p_0", "@_p_1", "@_p_2" }, 7, 8, 4);
        Assert.Equal(pageSql, repeated.CommandText);
        AssertParameters(repeated, new[] { "@_p_0", "@_p_1", "@_p_2" }, 7, 2, 2);
        foreach (var plan in new[] { source, page, repeated })
        {
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
            Assert.Equal(countSql, count.CommandText);
            AssertParameters(count, new[] { "@_p_0" }, 7);
        }
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>默认终结与已有渲染缓存都释放规划 Builder。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultSkipQuery_ShouldKeepIrAcrossRenderCache(bool cached)
    {
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new TestSqlBuilder().Select("Name").From("samples").OrderBy("Name").Skip(5));
        if (cached) query.ToSql();
        var source = query.GetPlan();
        Assert.Null(source.PlanningBuilder);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        AssertParameters(page, new[] { "@_p_0", "@_p_1" }, 8, 4);
        AssertParameters(source, new[] { "@_p_0" }, 5);
    }

    /// <summary>已有 offset 只需一个新增槽位；超限必须发生在值转换之前。</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void SkipOnlyPage_ShouldCheckOnlyMissingSlotBeforeConversion(int maximum)
    {
        var dialect = new IntegerDialect();
        var manager = new AdvancedParameterLimitManager(
            new AdvancedParameterLimitManager(new ParameterManager(dialect), maximum, "inner"), 4, "outer");
        var frozen = SqlQueryPlan.Create(new TestSqlBuilder(parameterManager: manager).Select("Name")
            .From("samples").Where("Status", 7).OrderBy("Name").Skip(5)).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        dialect.Calls = 0;
        if (maximum == 2)
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4)));
            Assert.Equal("SQL Provider 'inner' 的参数数量超出上限。当前参数数量: 2；尝试添加后数量: 3；最大参数数量: 2。", error.Message);
            Assert.Equal(0, dialect.Calls);
        }
        else
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
            AssertParameters(page, new[] { "@_p_0", "@_p_1", "@_p_2" }, 107, 108, 104);
            Assert.Equal(2, dialect.Calls);
        }
        AssertParameters(source, new[] { "@_p_0", "@_p_1" }, 107, 105);
    }

    /// <summary>偏移槽位可在筛选前分配；新 limit 沿用游标和大小写冲突规则。</summary>
    [Fact]
    public void SkipOnlyPage_ShouldReuseOffsetAndPreviewSingleName()
    {
        var manager = new ParameterManager(TestDialect.Instance);
        var builder = new TestSqlBuilder(parameterManager: manager).Select("Name").From("samples")
            .Skip(5).Where("Status", 7).OrderBy("Name");
        Assert.Equal("@_p_2", manager.GenerateName());
        manager.Add("@_P_3", 9);
        builder.AppendWhere("[Other]=@_P_3");
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_1 And [Other]=@_P_3 \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_4 Rows Only", page.CommandText);
        AssertParameters(page, new[] { "@_p_0", "@_p_1", "@_P_3", "@_p_4" }, 8, 7, 9, 4);
        AssertParameters(source, new[] { "@_p_0", "@_p_1", "@_P_3" }, 5, 7, 9);
        Assert.Equal("@_p_4", manager.GenerateName());
    }

    /// <summary>同一 IR 并发派生各自更新 offset，不回写源参数。</summary>
    [Fact]
    public async Task SkipOnlyPage_ShouldIsolateConcurrentRequests()
    {
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("samples").Skip(5)).Freeze();
        Assert.Null(source.PlanningBuilder);
        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(number, 4, "Name Desc"));
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] Desc \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
            AssertParameters(page, new[] { "@_p_0", "@_p_1" }, (number - 1) * 4, 4);
            Assert.IsType<SqlParam[]>(page.Parameters)[0].Value = 99;
        })));
        Assert.Equal("Select [Name] \r\nFrom [samples]", source.CommandText);
        AssertParameters(source, new[] { "@_p_0" }, 5);
    }

    private static void AssertParameters(SqlQueryPlan plan, string[] names, params object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(names, parameters.Select(p => p.Name));
        Assert.Equal(values, parameters.Select(p => p.Value));
    }

    private sealed class IntegerDialect : DialectBase
    {
        internal int Calls;
        public override object GetParamValue(object value) { Calls++; return value is int i ? i + 100 : value; }
    }
    private sealed class RejectBuilderSource : ISqlQueryBuilderSource
    {
        public ISqlBuilder CreateIndependentSqlBuilder() => throw new InvalidOperationException("IR 不应创建 Builder。");
    }
    private sealed class RenderCalls { internal int Clones; internal int Renders; }
    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly RenderCalls _calls;
        internal CountedBuilder(RenderCalls calls, IParameterManager parameters = null)
            : base(parameterManager: parameters) => _calls = calls;
        public override ISqlBuilder Clone() { _calls.Clones++; return base.Clone(); }
        public override string ToSql() { _calls.Renders++; return base.ToSql(); }
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameters) => new CountedBuilder(_calls, parameters);
    }
}
