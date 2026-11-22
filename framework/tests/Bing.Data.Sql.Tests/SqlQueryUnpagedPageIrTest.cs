using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>未分页有序查询直接使用冻结 IR 派生页面。</summary>
public sealed class SqlQueryUnpagedPageIrTest
{
    public static IEnumerable<object[]> OrderedShapes()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var explicitOrder in new[] { false, true })
        foreach (var providerOverload in new[] { false, true })
            yield return new object[] { shape, explicitOrder, providerOverload };
    }

    /// <summary>新增分页、重复分页和计数不复制或重渲染 Builder。</summary>
    [Theory]
    [MemberData(nameof(OrderedShapes))]
    public void OrderedQuery_ShouldDerivePageWithoutCloneOrRender(string shape, bool explicitOrder, bool providerOverload)
    {
        var calls = new RenderCalls();
        var builder = new CountedBuilder(calls);
        if (shape == "aggregate") builder.CountAll("Total");
        else builder.Select("Name");
        if (shape == "distinct") builder.Distinct();
        if (shape == "group") builder.GroupBy("Name");
        builder.From("samples").Where("Status", 7).OrderBy("Name");
        var source = SqlQueryPlan.Create(builder).Freeze();
        calls.Clones = calls.Renders = 0;

        var pager = new Pager(3, 4, explicitOrder ? "Name" : null);
        var page = providerOverload
            ? SqlBuilderRuntimeBridge.CreatePagePlan(source, pager, new TestSqlBuilder())
            : SqlBuilderRuntimeBridge.CreatePagePlan(source, pager);
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        var select = shape == "aggregate" ? "Select Count(*) As [Total]" :
            shape == "distinct" ? "Select Distinct [Name]" : "Select [Name]";
        var group = shape == "group" ? " \r\nGroup By [Name]" : "";
        const string body = "From [samples] \r\nWhere [Status]=@_p_0";
        var sourceSql = $"{select} \r\n{body}{group} \r\nOrder By [Name]";
        var pageSql = sourceSql + " \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only";
        Assert.Equal(pageSql, page.CommandText);
        Assert.Equal(pageSql, repeated.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Parameters(page).Select(p => p.Value));
        Assert.Equal(new object[] { 7, 2, 2 }, Parameters(repeated).Select(p => p.Value));
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(new object[] { 7 }, Parameters(source).Select(p => p.Value));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        Assert.Equal(shape == "simple" ? $"Select Count(*) \r\n{body}" :
            $"Select Count(*) \r\nFrom ({select} \r\n{body}{group}) As [t]", count.CommandText);
        Assert.Equal(new object[] { 7 }, Parameters(count).Select(p => p.Value));
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>普通终结计划和缓存渲染均保留追加分页所需的结构。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultOrderedQuery_ShouldDeriveFromUnpagedIr(bool cached)
    {
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new TestSqlBuilder().Select("Name").From("samples").OrderBy("Name"));
        if (cached) query.ToSql();
        var source = query.GetPlan();
        Assert.Null(source.PlanningBuilder);

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 8, 4 }, Parameters(page).Select(p => p.Value));
        Assert.Empty(Parameters(source));
    }

    /// <summary>名称预览沿用游标并避让手动参数，不改变原容器游标。</summary>
    [Fact]
    public void OrderedQuery_ShouldKeepNameCursorAndAvoidManualCollision()
    {
        var manager = new ParameterManager(TestDialect.Instance);
        Assert.Equal("@_p_0", manager.GenerateName());
        manager.Add("@_P_1", 7);
        var source = PureIr(new TestSqlBuilder(parameterManager: manager).Select("Name").From("samples")
            .AppendWhere("[Status]=@_P_1").OrderBy("Name"));

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_P_1 \r\nOrder By [Name] \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only", page.CommandText);
        Assert.Equal(new[] { "@_P_1", "@_p_2", "@_p_3" }, Parameters(page).Select(p => p.Name));
        Assert.Equal(new object[] { 7, 3, 3 }, Parameters(page).Select(p => p.Value));
        Assert.Equal("@_p_2", manager.GenerateName());
    }

    /// <summary>只使用参数容器的实际方言转换两个新增值。</summary>
    [Fact]
    public void OrderedQuery_ShouldConvertNewPageValuesOnce()
    {
        var dialect = new IntegerDialect();
        var source = PureIr(new TestSqlBuilder(parameterManager: new ParameterManager(dialect))
            .Select("Name").From("samples").Where("Status", 7).OrderBy("Name"));
        dialect.Calls = 0;

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 107, 108, 104 }, Parameters(page).Select(p => p.Value));
        Assert.Equal(2, dialect.Calls);
        Assert.Equal(107, Parameters(source).Single().Value);
    }

    /// <summary>分页上限同时覆盖第一和第二个新增槽位，保留最严格包装器诊断。</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OrderedQuery_ShouldKeepNestedParameterLimit(int maximum)
    {
        var dialect = new IntegerDialect();
        var inner = new AdvancedParameterLimitManager(new ParameterManager(dialect), maximum, "inner");
        var manager = new AdvancedParameterLimitManager(inner, 4, "outer");
        var source = PureIr(new TestSqlBuilder(parameterManager: manager)
            .Select("Name").From("samples").Where("Status", 7).OrderBy("Name"));
        dialect.Calls = 0;

        if (maximum == 3)
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
            Assert.Equal(new object[] { 107, 108, 104 }, Parameters(page).Select(p => p.Value));
        }
        else
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4)));
            Assert.Equal($"SQL Provider 'inner' 的参数数量超出上限。当前参数数量: {maximum}；尝试添加后数量: {maximum + 1}；最大参数数量: {maximum}。", error.Message);
            Assert.Equal(0, dialect.Calls);
        }
        Assert.Equal(107, Parameters(source).Single().Value);
    }

    /// <summary>外层新增分页不得改动子查询分页和参数。</summary>
    [Fact]
    public void OrderedQuery_ShouldKeepNestedPage()
    {
        var nested = new TestSqlBuilder().Select("Name").From("archive").OrderBy("Name").Skip(1).Take(3);
        var source = PureIr(new TestSqlBuilder().Select("Name").From(nested, "a").OrderBy("Name"));
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));

        Assert.Equal("Select [Name] \r\nFrom (Select [Name] \r\nFrom [archive] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only) As [a] \r\nOrder By [Name] \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 1, 3, 8, 4 }, Parameters(page).Select(p => p.Value));
        Assert.Equal(new object[] { 1, 3 }, Parameters(source).Select(p => p.Value));
    }

    /// <summary>并发派生隔离数组和元数据，调试快照也保留分页与上下文。</summary>
    [Fact]
    public async Task OrderedDebugQuery_ShouldIsolateConcurrentPagesAndContext()
    {
        var builder = new TestSqlBuilder().Select("Name").From("samples");
        ((IAdvancedParameterManager)((ISqlCommonPartAccessor)builder).ParameterManager)
            .Add(new SqlParam("@payload", new byte[] { 1, 2 }, DbType.Binary, size: 2));
        builder.AppendWhere("[Payload]=@payload").OrderBy("Name");
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var debug = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(frozen, true);
        var source = SqlQueryPlan.Create(SqlQueryIr.Create(debug, CommandType.Text, null), "split");
        source.SetContext("query-109", "parent-109", "Count");

        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var pager = new Pager(number, 4);
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, pager);
            pager.Page = 99;
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
            Assert.Equal("query-109", page.QueryContextId);
            Assert.Equal("parent-109", page.ParentQueryContextId);
            Assert.Equal("Data", page.Phase);
            Assert.Equal("split", page.SplitOn);
            Assert.Equal(number, page.QueryIr.CreateDefaultPagerSnapshot().Page);
            var parameters = Parameters(page);
            Assert.Equal(DbType.Binary, parameters[0].DbType);
            Assert.Equal(2, parameters[0].Size);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameters[0].Value));
            Assert.Equal(new object[] { (number - 1) * 4, 4 }, parameters.Skip(1).Select(p => p.Value));
            Assert.IsType<byte[]>(parameters[0].Value)[0] = 9;
            Assert.False(SqlBuilderRuntimeBridge.CreateExecutionSnapshot(page).HasDebugSql);
        })));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Parameters(source).Single().Value));
    }

    /// <summary>不同排序沿用追加规则，未确认的分页能力仍被拒绝。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrderedQuery_ShouldKeepOrderAndCapability(bool unsupported)
    {
        var options = new SqlOptions { QueryCapabilities = new SqlQueryCapabilities
        { Pagination = unsupported ? SqlQueryCapabilityState.Unsupported : SqlQueryCapabilityState.Supported } };
        var source = SqlQueryPlan.Create(new TestSqlBuilder(options: options).Select("Name").From("samples")
            .OrderBy("Name")).Freeze();
        if (unsupported)
        {
            var error = Assert.Throws<NotSupportedException>(() =>
                SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4)));
            Assert.Contains("分页", error.Message);
        }
        else
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Id"));
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name],[Id] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        }
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name]", source.CommandText);
        Assert.Empty(Parameters(source));
    }

    private static SqlQueryPlan PureIr(ISqlBuilder builder)
    {
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        return SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
    }

    /// <summary>只有 Skip 的半成品分页保留原参数名，不留下多余的偏移参数。</summary>
    [Fact]
    public void SkipOnlyQuery_ShouldReuseOffset()
    {
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("samples")
            .OrderBy("Name").Skip(5)).Freeze();
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 8, 4 }, Parameters(page).Select(p => p.Value));
        Assert.Equal(new object[] { 5 }, Parameters(source).Select(p => p.Value));
        Assert.Empty(Parameters(SqlBuilderRuntimeBridge.CreateCountPlan(page)));
    }

    private static SqlParam[] Parameters(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters);
    private sealed class IntegerDialect : DialectBase
    {
        internal int Calls;
        public override object GetParamValue(object value) { Calls++; return value is int i ? i + 100 : value; }
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
