using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>验证已有分页的冻结查询只更新参数，不再依赖规划 Builder。</summary>
public sealed class SqlQueryPageIrTest
{
    public static IEnumerable<object[]> FrozenPageCases()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var explicitOrder in new[] { false, true })
        foreach (var providerOverload in new[] { false, true })
            yield return new object[] { shape, explicitOrder, providerOverload };
    }

    /// <summary>两种分页入口复用四类 Select 结构，重复派生也不能复制或渲染 Builder。</summary>
    [Theory]
    [MemberData(nameof(FrozenPageCases))]
    public void FrozenPage_ShouldReuseSqlWithoutCloneOrRender(string shape, bool explicitOrder, bool providerOverload)
    {
        var calls = new RenderCalls();
        var builder = new CountedBuilder(calls);
        if (shape == "aggregate")
            builder.CountAll("Total");
        else
            builder.Select("Name");
        if (shape == "distinct")
            builder.Distinct();
        if (shape == "group")
            builder.GroupBy("Name");
        builder.From("samples").Where("Status", 7).OrderBy("Name").Skip(5).Take(2);
        var plan = SqlQueryPlan.Create(builder).Freeze();
        calls.Clones = calls.Renders = 0;

        var pager = new Pager(3, 4, explicitOrder ? "Name" : null);
        var page = providerOverload
            ? SqlBuilderRuntimeBridge.CreatePagePlan(plan, pager, new TestSqlBuilder())
            : SqlBuilderRuntimeBridge.CreatePagePlan(plan, pager);
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));

        var select = shape == "aggregate" ? "Select Count(*) As [Total]" :
            shape == "distinct" ? "Select Distinct [Name]" : "Select [Name]";
        var group = shape == "group" ? " \r\nGroup By [Name]" : "";
        var body = "From [samples] \r\nWhere [Status]=@_p_0";
        var sql = $"{select} \r\n{body}{group} \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only";
        Assert.Equal(sql, page.CommandText);
        Assert.Equal(sql, repeated.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Parameters(page).Select(item => item.Value));
        Assert.Equal(new object[] { 7, 2, 2 }, Parameters(repeated).Select(item => item.Value));
        Assert.Equal(new object[] { 7, 5, 2 }, Parameters(plan).Select(item => item.Value));
        Assert.Equal(shape == "simple" ? $"Select Count(*) \r\n{body}" :
            $"Select Count(*) \r\nFrom ({select} \r\n{body}{group}) As [t]",
            SqlBuilderRuntimeBridge.CreateCountPlan(page).CommandText);
        Assert.Null(page.PlanningBuilder);
        Assert.Null(repeated.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>普通查询即使没有保留规划源，也可以从首次渲染或缓存结构派生页面。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultQuery_WithExistingPage_ShouldUseIrWithoutPlanningSource(bool cached)
    {
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new TestSqlBuilder().Select("Name").From("samples").OrderBy("Name").Skip(5).Take(2));
        if (cached)
            query.ToSql();
        var plan = query.GetPlan();
        Assert.Null(plan.PlanningBuilder);

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(3, 4));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 8, 4 }, Parameters(page).Select(item => item.Value));
        Assert.Equal(new object[] { 5, 2 }, Parameters(plan).Select(item => item.Value));
    }

    /// <summary>不同排序按既有语义追加列，直接使用冻结 IR。</summary>
    [Fact]
    public void FrozenPage_WithDifferentOrder_ShouldUseIr()
    {
        var calls = new RenderCalls();
        var plan = SqlQueryPlan.Create(new CountedBuilder(calls).Select("Name").From("samples")
            .OrderBy("Name").Skip(5).Take(2)).Freeze();
        calls.Clones = calls.Renders = 0;

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(3, 4, "Id"));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name],[Id] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 8, 4 }, Parameters(page).Select(item => item.Value));
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>只转换两个新分页值，且使用参数管理器的实际方言，不重复转换筛选参数。</summary>
    [Fact]
    public void FrozenPage_ShouldConvertNewValuesOnceUsingParameterDialect()
    {
        var dialect = new IntegerDialect();
        var builder = new TestSqlBuilder(parameterManager: new ParameterManager(dialect))
            .Select("Name").From("samples").Where("Status", 7).OrderBy("Name").Skip(5).Take(2);
        var plan = SqlQueryPlan.Create(builder).Freeze();
        var original = Parameters(plan).Select(item => item.Value).ToArray();
        dialect.Calls = 0;

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(3, 4));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new[] { original[0], 108, 104 }, Parameters(page).Select(item => item.Value));
        Assert.Equal(original, Parameters(plan).Select(item => item.Value));
        Assert.Equal(2, dialect.Calls);
    }

    /// <summary>更新外层页面不得改写子查询的分页值。</summary>
    [Fact]
    public void FrozenPage_ShouldKeepNestedPagination()
    {
        var nested = new TestSqlBuilder().Select("Name").From("archive").OrderBy("Name").Skip(1).Take(3);
        var frozen = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From(nested, "a")
            .OrderBy("Name").Skip(5).Take(2)).Freeze();
        var plan = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(3, 4));

        Assert.Equal("Select [Name] \r\nFrom (Select [Name] \r\nFrom [archive] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only) As [a] \r\nOrder By [Name] \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 1, 3, 8, 4 }, Parameters(page).Select(item => item.Value));
        Assert.Equal(new object[] { 1, 3, 5, 2 }, Parameters(plan).Select(item => item.Value));
    }

    /// <summary>并发页面保留筛选元数据与数组快照，各次分页参数和公开参数副本互不污染。</summary>
    [Fact]
    public async Task FrozenPage_ConcurrentDerivation_ShouldIsolateParametersAndMetadata()
    {
        var payload = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder().Select("Name").From("samples");
        var manager = Assert.IsAssignableFrom<IAdvancedParameterManager>(((ISqlCommonPartAccessor)builder).ParameterManager);
        manager.Add(new SqlParam("@payload", payload, DbType.Binary, size: 2));
        builder.AppendWhere("[Payload]=@payload").OrderBy("Name").Skip(5).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var plan = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        payload[0] = 9;

        await Task.WhenAll(Enumerable.Range(1, 16).Select(pageNumber => Task.Run(() =>
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(pageNumber, 4));
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
            var parameters = Parameters(page);
            Assert.Equal(DbType.Binary, parameters[0].DbType);
            Assert.Equal(2, parameters[0].Size);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameters[0].Value));
            Assert.Equal((pageNumber - 1) * 4, parameters[1].Value);
            Assert.Equal(4, parameters[2].Value);
            Assert.IsType<byte[]>(parameters[0].Value)[0] = 8;
        })));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Parameters(plan)[0].Value));
        Assert.Equal(new object[] { 5, 2 }, Parameters(plan).Skip(1).Select(item => item.Value));
    }

    /// <summary>调试快照保留派生能力，页面复制执行上下文并冻结调用方 Pager。</summary>
    [Fact]
    public void Page_FromDebugSnapshot_ShouldKeepContextAndIndependentPager()
    {
        var frozen = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("samples")
            .OrderBy("Name").Skip(5).Take(2)).Freeze();
        var debugSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(frozen, true);
        var plan = SqlQueryPlan.Create(SqlQueryIr.Create(debugSnapshot, CommandType.Text, null), "split");
        plan.SetContext("query-107", "parent-107", "Count");
        var pager = new Pager(3, 4);

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, pager);
        pager.Page = 9;
        pager.PageSize = 1;

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 8, 4 }, Parameters(page).Select(item => item.Value));
        Assert.Equal("query-107", page.QueryContextId);
        Assert.Equal("parent-107", page.ParentQueryContextId);
        Assert.Equal("Data", page.Phase);
        Assert.Equal("split", page.SplitOn);
        var defaultPager = page.QueryIr.CreateDefaultPagerSnapshot();
        Assert.Equal(3, defaultPager.Page);
        Assert.Equal(4, defaultPager.PageSize);
        Assert.False(SqlBuilderRuntimeBridge.CreateExecutionSnapshot(page).HasDebugSql);
    }

    private static SqlParam[] Parameters(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters);

    private sealed class IntegerDialect : DialectBase
    {
        internal int Calls;
        public override object GetParamValue(object value)
        {
            Calls++;
            return value is int number ? number + 100 : value;
        }
    }

    private sealed class RenderCalls
    {
        internal int Clones;
        internal int Renders;
    }

    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly RenderCalls _calls;
        internal CountedBuilder(RenderCalls calls, IParameterManager parameters = null)
            : base(parameterManager: parameters) => _calls = calls;
        public override ISqlBuilder Clone() { _calls.Clones++; return base.Clone(); }
        public override string ToSql() { _calls.Renders++; return base.ToSql(); }
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) => new CountedBuilder(_calls, parameterManager);
    }
}
