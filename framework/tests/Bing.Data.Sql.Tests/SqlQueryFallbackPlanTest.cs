using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>不可完整表达为 IR 的扩展查询在多次派生时应保留隔离的结构来源。</summary>
public sealed class SqlQueryFallbackPlanTest
{
    /// <summary>直接调用重写的 ToSql 时，base 只返回基础 SQL，避免扩展尾部重复追加。</summary>
    [Fact]
    public void DirectToSql_ShouldApplyFilterAndOverrideOncePerRender()
    {
        var filter = new SequenceFilter();
        var builder = new TailBuilder(new SqlBuilderServices(filters: new[] { filter }))
            .Select("Name").From<FilterRow>("s");
        for (var execution = 1; execution <= 2; execution++)
        {
            Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 /* extension */", builder.ToSql());
            Assert.Equal(execution, filter.Calls);
            Assert.Empty(builder.GetParams());
        }
    }

    /// <summary>直接快照入口保留 ToSql 扩展，重复渲染重新取过滤值且不修改输入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutionSnapshot_ShouldPreserveToSqlOverrideAndFilterIsolation(bool includeDebugSql)
    {
        var filter = new SequenceFilter();
        var builder = new TailBuilder(new SqlBuilderServices(filters: new[] { filter }))
            .Select("Name").From<FilterRow>("s");
        for (var execution = 1; execution <= 2; execution++)
        {
            var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql);
            Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 /* extension */", snapshot.Sql);
            Assert.Equal(execution, Assert.Single(snapshot.Parameters).Value);
            if (includeDebugSql)
                Assert.Equal($"Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]={execution} /* extension */", snapshot.DebugSql);
            Assert.Equal(execution, filter.Calls);
            Assert.Empty(builder.GetParams());
        }
    }

    /// <summary>共享回退计划的不同派生请求只修改自己的 Builder 和参数副本。</summary>
    [Fact]
    public async Task ConcurrentFallbackPages_ShouldKeepSourceAndParameterValuesIndependent()
    {
        var payload = new byte[] { 1, 2 };
        var source = SqlQueryPlan.Create(new TailBuilder().Select("Name").From("samples")
            .Where("Payload", payload).OrderBy("Id")).Freeze();
        payload[0] = 99;
        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(number, 2, "Id"));
            var next = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(number + 1, 1, "Id"));
            const string pageSql = "Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only /* extension */";
            Assert.Equal(pageSql, page.CommandText);
            Assert.Equal(pageSql, next.CommandText);
            var parameters = Assert.IsType<SqlParam[]>(next.Parameters);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameters[0].Value));
            Assert.Equal(number, parameters[1].Value);
            Assert.Equal(1, parameters[2].Value);
            Assert.IsType<byte[]>(parameters[0].Value)[0] = 88;
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(next);
            Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 /* extension */", count.CommandText);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value));
        })));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nOrder By [Id] /* extension */", source.CommandText);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(Assert.IsType<SqlParam[]>(source.Parameters)).Value));
    }

    /// <summary>保留回退来源时必须保留本次过滤结果，重复执行查询仍会重新获取动态值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultFallbackQuery_ShouldKeepPerExecutionFilterSnapshot(bool forPaging)
    {
        var filter = new SequenceFilter();
        var services = new SqlBuilderServices(filters: new[] { filter });
        var builder = new TailBuilder(services).Select("Name").From<FilterRow>("s");
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var plans = new List<SqlQueryPlan>();
        for (var execution = 1; execution <= 2; execution++)
        {
            var plan = query.GetPlan(forPaging);
            plans.Add(plan);
            Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 /* extension */", plan.CommandText);
            AssertValues(plan, execution);
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(2, 1, "Name"));
            var next = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(3, 1, "Name"));
            const string pageSql = "Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only /* extension */";
            Assert.Equal(pageSql, page.CommandText);
            Assert.Equal(pageSql, next.CommandText);
            AssertValues(page, execution, 1, 1);
            AssertValues(next, execution, 2, 1);
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(next);
            Assert.Equal("Select Count(*) \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 /* extension */", count.CommandText);
            AssertValues(count, execution);
            Assert.Equal(execution, filter.Calls);
        }
        AssertValues(plans[0], 1);
        AssertValues(plans[1], 2);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>自定义 SQL 尾部在默认终结及显式冻结后，重复 Count/Page 都不能退化为文本包装。</summary>
    [Theory]
    [InlineData("freeze", false)]
    [InlineData("freeze", true)]
    [InlineData("query", false)]
    [InlineData("query", true)]
    [InlineData("paging", false)]
    [InlineData("paging", true)]
    public void CustomTail_ShouldKeepPlanningSourceAcrossDerivedPages(string entry, bool grouped)
    {
        var builder = new TailBuilder().Select("Name").From("samples").Where("Status", 7).OrderBy("Id");
        if (grouped)
            builder.GroupBy("Name");
        var plan = entry == "freeze"
            ? SqlQueryPlan.Create(builder).Freeze()
            : new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder).GetPlan(entry == "paging");
        var body = "Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0" +
            (grouped ? " \r\nGroup By [Name]" : string.Empty);
        var sourceSql = body + " \r\nOrder By [Id] /* extension */";
        var countSql = grouped
            ? "Select Count(*) \r\nFrom (" + body + " /* extension */) As [t] /* extension */"
            : "Select Count(*) \r\nFrom [samples] \r\nWhere [Status]=@_p_0 /* extension */";
        Assert.Equal(sourceSql, plan.CommandText);
        builder.Where("Changed", 99);

        for (var iteration = 0; iteration < 2; iteration++)
        {
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(2, 3, "Name Desc"));
            Assert.Equal(body + " \r\nOrder By [Id],[Name] Desc \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only /* extension */", page.CommandText);
            AssertValues(page, 7, 3, 3);
            Assert.Equal(countSql, SqlBuilderRuntimeBridge.CreateCountPlan(page).CommandText);

            var next = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(3, 2, "Status Desc"));
            Assert.Equal(body + " \r\nOrder By [Id],[Name] Desc,[Status] Desc \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only /* extension */", next.CommandText);
            AssertValues(next, 7, 4, 2);
            Assert.Equal(countSql, SqlBuilderRuntimeBridge.CreateCountPlan(next).CommandText);
            Assert.Equal(countSql, SqlBuilderRuntimeBridge.CreateCountPlan(plan).CommandText);
            Assert.Equal(sourceSql, plan.CommandText);
            AssertValues(plan, 7);
        }
    }

    private static void AssertValues(SqlQueryPlan plan, params object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(Enumerable.Range(0, values.Length).Select(index => $"@_p_{index}"), parameters.Select(item => item.Name));
        Assert.Equal(values, parameters.Select(item => item.Value));
        Assert.False(plan.IsBuilderPlan);
    }

    private sealed class TailBuilder : TestSqlBuilder
    {
        internal TailBuilder(SqlBuilderServices services = null, IParameterManager parameters = null)
            : base(services ?? new SqlBuilderServices(), TestDialect.Instance, parameters) { }
        public override string ToSql() => base.ToSql() + " /* extension */";
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) => new TailBuilder(Services, parameterManager);
    }

    [Table("samples")]
    private sealed class FilterRow { public int TenantId { get; set; } }

    private sealed class SequenceFilter : ISqlFilter
    {
        internal int Calls;
        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(FilterRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.TenantId)), ++Calls);
        }
    }
}
