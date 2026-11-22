using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>默认查询首次冻结后，静态执行复用 IR，执行状态继续按次创建。</summary>
public sealed class SqlQueryFrozenIrReuseTest
{
    public static IEnumerable<object[]> Shapes()
    {
        foreach (var shape in new[] { "simple", "group", "cte", "set" })
        foreach (var cached in new[] { false, true })
            yield return new object[] { shape, cached };
    }

    /// <summary>SQL 预览命中或未命中时，重复普通/分页终结都不能再渲染冻结结构。</summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void RepeatedTerminal_ShouldReuseIrWithoutRendering(string shape, bool cached)
    {
        var calls = new Calls();
        var builder = new CountedBuilder(calls);
        builder.Select("Name").From(shape == "cte" ? "recent" : "samples").Where("Status", 7).OrderBy("Name");
        if (shape == "group") builder.GroupBy("Name");
        if (shape == "cte") builder.With("recent", new TestSqlBuilder().Select("Name").From("archive").Where("Kind", 8));
        if (shape == "set") builder.UnionAll(new TestSqlBuilder().Select("Name").From("other").Where("Kind", 8));
        var query = Query(builder);
        var body = shape == "set"
            ? "(Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\n) \r\nUnion All \r\n(Select [Name] \r\nFrom [other] \r\nWhere [Kind]=@_p_1 \r\n)"
            : "Select [Name] \r\nFrom [" + (shape == "cte" ? "recent" : "samples") + "] \r\nWhere [Status]=@_p_0" +
              (shape == "group" ? " \r\nGroup By [Name]" : "");
        var prefix = shape == "cte" ? "With [recent] \r\nAs (Select [Name] \r\nFrom [archive] \r\nWhere [Kind]=@_p_1)\r\n" : "";
        var expected = prefix + body + " \r\nOrder By [Name]";
        if (cached) Assert.Equal(expected, query.ToSql());
        var first = query.GetPlan();
        Assert.Equal(expected, first.CommandText);
        Assert.Null(first.PlanningBuilder);
        calls.Clones = calls.Renders = 0;
        builder.Where("Changed", 99);

        var next = query.GetPlan(forPaging: true);
        var repeated = query.GetPlan();
        Assert.Equal(expected, next.CommandText);
        Assert.Equal(expected, repeated.CommandText);
        Assert.Same(first.QueryIr, next.QueryIr);
        Assert.Same(first.QueryIr, repeated.QueryIr);
        Assert.NotSame(first, next);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(next);
        Assert.Equal(shape is "group" or "set" ? prefix + "Select Count(*) \r\nFrom (" + body + ") As [t]"
            : prefix + "Select Count(*) \r\nFrom [" + (shape == "cte" ? "recent" : "samples") + "] \r\nWhere [Status]=@_p_0", count.CommandText);
        var slots = shape is "cte" or "set" ? 2 : 1;
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(next, new Pager(2, 3, "Name"));
        Assert.Equal(expected + $" \r\nOffset @_p_{slots} Rows Fetch Next @_p_{slots + 1} Rows Only", page.CommandText);
        Assert.Equal(slots == 2 ? new object[] { 7, 8, 3, 3 } : new object[] { 7, 3, 3 }, Values(page));
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>复用 IR 不能复用执行标识、生成执行标记或活动租约。</summary>
    [Fact]
    public void RepeatedTerminal_ShouldCreateIndependentExecutionState()
    {
        var query = Query(new CountedBuilder(new Calls()).Select("Name").From("samples"));
        var first = query.GetPlan();
        first.IsGeneratedExecution = true;
        first.NotifyExecutionStarted();
        var firstId = first.ExecutionId;
        first.NotifyExecutionFinished();
        var next = query.GetPlan(true);
        Assert.Same(first.QueryIr, next.QueryIr);
        Assert.Equal(first.QueryContextId, next.QueryContextId);
        Assert.Null(next.ExecutionId);
        Assert.False(next.IsGeneratedExecution);
        next.NotifyExecutionStarted();
        try
        {
            Assert.NotEqual(firstId, next.ExecutionId);
            Assert.Equal(firstId, first.ExecutionId);
            Assert.Throws<InvalidOperationException>(() => first.NotifyExecutionStarted());
        }
        finally { next.NotifyExecutionFinished(); }
        Assert.Throws<InvalidOperationException>(() => query.GetBuilder());
    }

    /// <summary>公开参数值副本、分页参数及元数据不能污染被复用的冻结 IR。</summary>
    [Fact]
    public void RepeatedTerminal_ShouldKeepParameterValuesAndMetadataFrozen()
    {
        var payload = new byte[] { 1, 2 };
        var builder = new CountedBuilder(new Calls());
        builder.Select("Name").From("samples").AppendWhere("[Payload]=@payload");
        var manager = (IAdvancedParameterManager)((ISqlCommonPartAccessor)builder).ParameterManager;
        manager.Add(new SqlParam("@payload", payload, DbType.Binary, size: 2));
        var query = Query(builder);
        var first = query.GetPlan();
        payload[0] = 9;
        var exposed = Assert.Single(Assert.IsType<SqlParam[]>(first.Parameters));
        Assert.IsType<byte[]>(exposed.Value)[0] = 8;
        exposed.Value = new byte[] { 8, 9 };
        var next = query.GetPlan();
        Assert.Same(first.QueryIr, next.QueryIr);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@payload", next.CommandText);
        var parameter = Assert.Single(Assert.IsType<SqlParam[]>(next.Parameters));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameter.Value));
        Assert.Equal(DbType.Binary, parameter.DbType);
        Assert.Equal(2, parameter.Size);
    }

    /// <summary>失败的首次渲染不能发布缓存；成功重试之后才能复用 IR。</summary>
    [Fact]
    public void FailedFirstRender_ShouldRetryBeforePublishingReusableIr()
    {
        var calls = new Calls { Fail = true };
        var query = Query(new CountedBuilder(calls).Select("Name").From("samples"));
        Assert.Throws<InvalidOperationException>(() => query.GetPlan());
        calls.Fail = false;
        var first = query.GetPlan();
        var next = query.GetPlan();
        Assert.Equal("Select [Name] \r\nFrom [samples]", next.CommandText);
        Assert.Same(first.QueryIr, next.QueryIr);
        Assert.Equal(2, calls.Renders);
    }

    /// <summary>动态过滤每次重新捕获；本次派生 Count/Page 不重复调用过滤器。</summary>
    [Fact]
    public void DynamicFilter_ShouldNotReuseExecutionValues()
    {
        var filter = new SequenceFilter();
        var query = Query(new CountedBuilder(new Calls(), new SqlBuilderServices(filters: new[] { filter }))
            .Select("Name").From<FilterRow>("s"));
        var first = query.GetPlan();
        var next = query.GetPlan(true);
        Assert.NotSame(first.QueryIr, next.QueryIr);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0", first.CommandText);
        Assert.Equal(first.CommandText, next.CommandText);
        Assert.Equal(new object[] { 1 }, Values(first));
        Assert.Equal(new object[] { 2 }, Values(next));
        Assert.Equal("Select Count(*) \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0", SqlBuilderRuntimeBridge.CreateCountPlan(next).CommandText);
        Assert.Equal(2, filter.Calls);
    }

    /// <summary>外层没有过滤器时，也不能复用 CTE/Union 后代本次捕获的动态值。</summary>
    [Theory]
    [InlineData("cte")]
    [InlineData("nested-cte")]
    [InlineData("set")]
    public void NestedDynamicFilter_ShouldCaptureValuesForEveryTerminal(string shape)
    {
        var filter = new SequenceFilter();
        var child = new CountedBuilder(new Calls(), new SqlBuilderServices(filters: new[] { filter }))
            .Select("Name").From<FilterRow>("s");
        var builder = new TestSqlBuilder().Select("Name").From(shape == "set" ? "samples" : "recent");
        if (shape == "set") builder.UnionAll(child);
        else if (shape == "cte") builder.With("recent", child);
        else builder.With("recent", new TestSqlBuilder().Select("Name").From("inner")
            .With("inner", child));
        var query = Query(builder);
        const string filtered = "Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0";
        var sql = shape switch
        {
            "cte" => "With [recent] \r\nAs (" + filtered + ")\r\nSelect [Name] \r\nFrom [recent]",
            "nested-cte" => "With [recent] \r\nAs (With [inner] \r\nAs (" + filtered + ")\r\nSelect [Name] \r\nFrom [inner])\r\nSelect [Name] \r\nFrom [recent]",
            _ => "(Select [Name] \r\nFrom [samples] \r\n) \r\nUnion All \r\n(" + filtered + " \r\n)"
        };
        var first = query.GetPlan();
        var next = query.GetPlan(true);
        Assert.Equal(sql, first.CommandText);
        Assert.Equal(sql, next.CommandText);
        Assert.NotSame(first.QueryIr, next.QueryIr);
        Assert.Equal(new object[] { 1 }, Values(first));
        Assert.Equal(new object[] { 2 }, Values(next));
        Assert.Equal(2, filter.Calls);
    }

    /// <summary>不能完整表达为 IR 的第三方 SQL 扩展仍按原合同渲染。</summary>
    [Fact]
    public void CustomSqlFallback_ShouldKeepPerExecutionRenderContract()
    {
        var calls = new Calls { Tail = " /* extension */" };
        var query = Query(new CountedBuilder(calls).Select("Name").From("samples"));
        var first = query.GetPlan();
        var next = query.GetPlan();
        Assert.NotSame(first.QueryIr, next.QueryIr);
        Assert.Equal("Select [Name] \r\nFrom [samples] /* extension */", first.CommandText);
        Assert.Equal(first.CommandText, next.CommandText);
        Assert.NotNull(next.PlanningBuilder);
        Assert.Equal(2, calls.Renders);
    }

    private static SqlQuery Query(ISqlBuilder builder) => new(new Mock<ISqlQueryPlanExecutor>().Object, builder);
    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(p => p.Value).ToArray();
    private sealed class Calls { internal int Clones, Renders; internal bool Fail; internal string Tail = ""; }
    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly Calls _calls;
        internal CountedBuilder(Calls calls, SqlBuilderServices services = null, IParameterManager parameters = null)
            : base(services ?? new SqlBuilderServices(), TestDialect.Instance, parameters) => _calls = calls;
        public override ISqlBuilder Clone() { _calls.Clones++; return base.Clone(); }
        public override string ToSql()
        {
            _calls.Renders++;
            if (_calls.Fail) throw new InvalidOperationException("render failed");
            return base.ToSql() + _calls.Tail;
        }
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) => new CountedBuilder(_calls, Services, parameterManager);
    }
    [Table("samples")]
    private sealed class FilterRow { public int TenantId { get; set; } }
    private sealed class SequenceFilter : ISqlFilter
    {
        internal int Calls;
        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(s => s.EntityType == typeof(FilterRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.TenantId)), ++Calls);
        }
    }
}
