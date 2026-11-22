using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>验证冻结计划派生时的隔离、动态过滤一致性与 Builder 复制预算。</summary>
public sealed class SqlFrozenPlanDerivationTest
{
    /// <summary>未冻结 Count 只用一个工作副本；冻结后直接按 IR 派生，不再复制 Builder。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Count_ShouldUseIndependentDraftOrFrozenIr(bool frozen, bool grouped)
    {
        var counter = new CloneCounter();
        var builder = CreateBuilder(counter, grouped);
        var plan = SqlQueryPlan.Create(builder);
        if (frozen)
            plan = plan.Freeze();
        var originalSql = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan).Sql;
        counter.Calls = 0;

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        Assert.Equal(frozen ? 0 : 1, counter.Calls);
        Assert.False(count.IsBuilderPlan);
        Assert.Null(count.PlanningBuilder);
        Assert.Equal(grouped
            ? "Select Count(*) \r\nFrom (Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nGroup By [Name]) As [t]"
            : "Select Count(*) \r\nFrom [samples] \r\nWhere [Status]=@_p_0", count.CommandText);
        var parameter = Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters));
        Assert.Equal("@_p_0", parameter.Name);
        Assert.Equal(7, parameter.Value);
        Assert.Equal(originalSql, SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan).Sql);
    }

    /// <summary>未冻结页面使用独立草稿，冻结页面沿用既有排序和 SQL，不再复制 Builder。</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Page_ShouldUseIndependentDraftOrFrozenIr(bool frozen, bool grouped, bool providerOverload)
    {
        var counter = new CloneCounter();
        var plan = SqlQueryPlan.Create(CreateBuilder(counter, grouped));
        if (frozen)
            plan = plan.Freeze();
        var originalSql = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan).Sql;
        counter.Calls = 0;
        var pager = new Pager(2, 3, "Name");

        var page = providerOverload
            ? SqlBuilderRuntimeBridge.CreatePagePlan(plan, pager, new TestSqlBuilder())
            : SqlBuilderRuntimeBridge.CreatePagePlan(plan, pager);

        Assert.Equal(frozen ? 0 : 1, counter.Calls);
        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0" +
            (grouped ? " \r\nGroup By [Name]" : string.Empty) +
            " \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(page.Parameters);
        Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2" }, parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 7, 3, 3 }, parameters.Select(item => item.Value));
        Assert.Equal(originalSql, SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan).Sql);
    }

    /// <summary>普通复杂查询的 SQL 与 Count/Page 派生源必须来自同一次动态过滤。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultComplexQuery_ShouldShareFilterSnapshotWithDerivedPlans(bool grouped)
    {
        var filter = new SequenceFilter();
        var builder = new TestSqlBuilder(new SqlBuilderServices(filters: new[] { filter }), TestDialect.Instance)
            .Select("Name").From<FilterRow>("s");
        if (grouped)
            builder.GroupBy("Name");
        else
            ((ISqlQueryClauseAccessor)builder).SelectClause.Distinct();
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);

        var plan = query.GetPlan();
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(1, 2, "Name"), new TestSqlBuilder());

        Assert.Equal(1, filter.Calls);
        var sourceSql = (grouped ? "Select [Name]" : "Select Distinct [Name]") +
            " \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0" +
            (grouped ? " \r\nGroup By [Name]" : string.Empty);
        Assert.Equal(sourceSql, plan.CommandText);
        Assert.Equal($"Select Count(*) \r\nFrom ({sourceSql}) As [t]", count.CommandText);
        Assert.Equal(sourceSql + " \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(1, Assert.Single(Assert.IsType<SqlParam[]>(plan.Parameters)).Value);
        Assert.Equal(1, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
        Assert.Equal(new object[] { 1, 0, 2 }, Assert.IsType<SqlParam[]>(page.Parameters).Select(item => item.Value));
    }

    private static ISqlBuilder CreateBuilder(CloneCounter counter, bool grouped)
    {
        var builder = new CountedBuilder(counter).Select("Name").From("samples").Where("Status", 7);
        if (grouped)
            builder.GroupBy("Name");
        return builder.OrderBy("Name").Skip(5).Take(2);
    }

    private sealed class CloneCounter
    {
        internal int Calls;
    }

    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly CloneCounter _counter;

        internal CountedBuilder(CloneCounter counter, IParameterManager parameters = null)
            : base(parameterManager: parameters) => _counter = counter;

        public override ISqlBuilder Clone()
        {
            _counter.Calls++;
            return base.Clone();
        }

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new CountedBuilder(_counter, parameterManager);
    }

    [Table("samples")]
    private sealed class FilterRow
    {
        public int TenantId { get; set; }
    }

    private sealed class SequenceFilter : ISqlFilter
    {
        internal int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.SingleOrDefault(item => item.EntityType == typeof(FilterRow));
            if (source == null)
                return;
            Calls++;
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.TenantId)), Calls);
        }
    }
}
