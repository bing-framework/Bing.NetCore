using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>冻结后的预览、读取和克隆统一使用隔离结构，失败不发布结构。</summary>
public sealed class SqlQueryFrozenLifecycleTest
{
    /// <summary>预览缓存的命中与未命中均不能让冻结后的读取返回原草稿。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstTerminal_ShouldReplaceDraftForAllReadPaths(bool previewed)
    {
        var source = new TestSqlBuilder().Select("Name").From("samples").Where("Status", 7);
        var query = Query(source);
        const string sql = "Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0";
        if (previewed) Assert.Equal(sql, query.ToSql());
        var first = query.GetPlan();
        source.Where("Changed", 99);

        Assert.Equal(sql, query.ToSql());
        Assert.NotSame(source, query.ReadBuilder);
        Assert.Same(query.ReadBuilder, ((ISqlQueryBuilderAccessor)query).GetSqlBuilder());
        Assert.Equal(sql, query.ReadBuilder.ToSql());
        Assert.Equal(new object[] { 7 }, Values(first));
        Assert.Same(first.QueryIr, query.GetPlan(true).QueryIr);
    }

    /// <summary>取消也固定结构；克隆继承冻结结构后仍能独立追加条件。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloneAfterFreeze_ShouldBranchFromFrozenStructure(bool cancelled)
    {
        var source = new TestSqlBuilder().Select("Name").From("samples").Where("Status", 7);
        var query = Query(source);
        if (cancelled) query.GetPlan(new CancellationToken(true));
        else query.GetPlan();
        source.Where("Changed", 99);

        var clone = query.Clone();
        clone.GetBuilder().Where("Kind", 8);
        var plan = clone.GetPlan();
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 And [Kind]=@_p_1", plan.CommandText);
        Assert.Equal(new object[] { 7, 8 }, Values(plan));
        Assert.Equal(query.QueryContextId, plan.ParentQueryContextId);
        Assert.NotEqual(query.QueryContextId, plan.QueryContextId);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0", query.GetPlan().CommandText);
        Assert.Throws<InvalidOperationException>(() => query.GetBuilder());
    }

    /// <summary>首次渲染失败时，后续修改必须参与成功重试，不能使用失败时的旧副本。</summary>
    [Fact]
    public void FailedFirstTerminal_ShouldKeepDraftEditableUntilSuccessfulRetry()
    {
        var calls = new Calls { Fail = true };
        var source = new FailingBuilder(calls);
        source.Select("Name").From("samples").Where("Status", 7);
        var query = Query(source);
        Assert.Throws<InvalidOperationException>(() => query.GetPlan());
        Assert.Same(source, query.GetBuilder());
        source.Where("Kind", 8);
        calls.Fail = false;

        var plan = query.GetPlan();
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 And [Kind]=@_p_1", plan.CommandText);
        Assert.Equal(new object[] { 7, 8 }, Values(plan));
        Assert.NotSame(source, query.ReadBuilder);
    }

    /// <summary>动态过滤既可在根也可在 CTE；预览按次取值，但不能污染后续终结。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenPreview_ShouldRefreshDynamicValuesWithoutChangingStructure(bool nested)
    {
        var filter = new SequenceFilter();
        var child = new TestSqlBuilder(new SqlBuilderServices(filters: new[] { filter }), TestDialect.Instance)
            .Select("Name").From<FilterRow>("s");
        var source = nested ? new TestSqlBuilder().Select("Name").From("recent").With("recent", child) : child;
        var query = Query(source);
        var filtered = "Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0";
        var sql = nested ? "With [recent] \r\nAs (" + filtered + ")\r\nSelect [Name] \r\nFrom [recent]" : filtered;
        var first = query.GetPlan();
        Assert.Equal(new object[] { 1 }, Values(first));
        source.Where("Changed", 99);
        Assert.Equal(sql, query.ToSql());
        Assert.Equal(2, filter.Calls);
        Assert.Equal(sql, query.ToSql());
        Assert.Equal(3, filter.Calls);
        var next = query.GetPlan(true);
        Assert.Equal(sql, next.CommandText);
        Assert.Equal(new object[] { 4 }, Values(next));
        Assert.Equal(new object[] { 1 }, Values(first));
    }

    private static SqlQuery Query(ISqlBuilder source) => new(new Mock<ISqlQueryPlanExecutor>().Object, source);
    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(p => p.Value).ToArray();
    private sealed class Calls { internal bool Fail; }
    private sealed class FailingBuilder : TestSqlBuilder
    {
        private readonly Calls _calls;
        internal FailingBuilder(Calls calls, IParameterManager parameters = null)
            : base(new SqlBuilderServices(), TestDialect.Instance, parameters) => _calls = calls;
        public override string ToSql() => _calls.Fail ? throw new InvalidOperationException("render failed") : base.ToSql();
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) => new FailingBuilder(_calls, parameterManager);
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
