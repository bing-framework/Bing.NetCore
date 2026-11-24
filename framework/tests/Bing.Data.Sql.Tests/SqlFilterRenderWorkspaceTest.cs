using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>动态过滤的执行快照应隔离渲染状态，并只复制实际变更的查询部分。</summary>
public sealed class SqlFilterRenderWorkspaceTest
{
    /// <summary>创建动态执行快照时不应克隆整棵 Builder，且 SQL、调试文本、参数来自同次过滤。</summary>
    [Fact]
    public void CreateExecutionSnapshot_WhenDynamicFilterIsApplied_ShouldKeepBuilderAndClausesUnchanged()
    {
        var filter = new SequenceFilter();
        var calls = new BuilderCalls();
        var builder = new CountedSqlServerBuilder(calls,
            CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s");
        builder.AppendJoin("[logs] On [s].[Id]=[logs].[SampleId]");
        builder.Where("Status", 7).GroupBy("Name").OrderBy("Name");

        var select = builder.SelectClause;
        var from = builder.FromClause;
        var join = builder.JoinClause;
        var where = builder.WhereClause;
        var groupBy = builder.GroupByClause;
        var orderBy = builder.OrderByClause;
        var parameters = builder.GetParams().OrderBy(item => item.Key).ToArray();
        calls.CloneCount = 0;

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);

        const string expectedSql = "Select [Name] \r\nFrom [samples] As [s] " +
            "\r\nJoin [logs] On [s].[Id]=[logs].[SampleId] \r\nWhere [Status]=@_p_0 And [s].[TenantId]=@_p_1 " +
            "\r\nGroup By [Name] \r\nOrder By [Name]";
        const string expectedDebugSql = "Select [Name] \r\nFrom [samples] As [s] " +
            "\r\nJoin [logs] On [s].[Id]=[logs].[SampleId] \r\nWhere [Status]=7 And [s].[TenantId]=1 " +
            "\r\nGroup By [Name] \r\nOrder By [Name]";

        Assert.Equal(expectedSql, snapshot.Sql);
        Assert.Equal(expectedDebugSql, snapshot.DebugSql);
        Assert.Equal(new object[] { 7, 1 }, snapshot.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(1, filter.Calls);
        Assert.Equal(0, calls.CloneCount);
        Assert.Same(select, builder.SelectClause);
        Assert.Same(from, builder.FromClause);
        Assert.Same(join, builder.JoinClause);
        Assert.Same(where, builder.WhereClause);
        Assert.Same(groupBy, builder.GroupByClause);
        Assert.Same(orderBy, builder.OrderByClause);
        Assert.Equal(parameters, builder.GetParams().OrderBy(item => item.Key));
    }

    /// <summary>连续执行快照应重新获取过滤值，且不将上次谓词累加到草稿。</summary>
    [Fact]
    public void CreateExecutionSnapshot_Repeatedly_ShouldUseFreshFilterValueWithoutAccumulatingPredicates()
    {
        var filter = new SequenceFilter();
        var builder = CreateFilteredBuilder(filter);

        var first = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);
        var second = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);

        const string expectedSql = "Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0";
        Assert.Equal(expectedSql, first.Sql);
        Assert.Equal(expectedSql, second.Sql);
        Assert.Equal(1, Assert.Single(first.Parameters).Value);
        Assert.Equal(2, Assert.Single(second.Parameters).Value);
        Assert.Equal(2, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>从首次计划派生的 Count 和 Page 应共用同一过滤值，之后取计划不再克隆已隔离草稿。</summary>
    [Fact]
    public void GetPlan_WhenCountAndPageAreDerived_ShouldReuseFilteredSnapshotWithoutCloningAgain()
    {
        var filter = new SequenceFilter();
        var calls = new BuilderCalls();
        var builder = new CountedSqlServerBuilder(calls, CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s").OrderBy("Name");
        calls.CloneCount = 0;
        var original = builder;
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);

        var firstPlan = query.GetPlan(forPaging: true);
        var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(firstPlan);
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(firstPlan, new Pager(2, 10, "Name"));
        var count = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(countPlan);
        var page = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(pagePlan);

        Assert.Equal("Select Count(*) \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0", count.Sql);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 " +
                     "\r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.Sql);
        Assert.Equal(new object[] { 1 }, count.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 1, 10, 10 }, page.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(1, filter.Calls);
        Assert.Equal(1, calls.CloneCount);
        Assert.Empty(original.GetParams());

        var nextPlan = query.GetPlan(forPaging: true);
        var next = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(nextPlan);

        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 " +
                     "\r\nOrder By [Name]", next.Sql);
        Assert.Equal(2, Assert.Single(next.Parameters).Value);
        Assert.Equal(2, filter.Calls);
        Assert.Equal(1, calls.CloneCount);
    }

    /// <summary>局部过滤快照应保留派生 Builder 的 CreateSql 尾部并让 Count/Page 共用首次过滤结果。</summary>
    [Fact]
    public void GetPlan_WhenOptedInBuilderOverridesCreateSql_ShouldPreserveTailAcrossCountAndPage()
    {
        var filter = new SequenceFilter();
        var builder = new TailSqlServerBuilder(CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s").OrderBy("Name");
        Assert.IsType<TailSqlServerBuilder>(builder.Clone());
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);

        var firstPlan = query.GetPlan(forPaging: true);
        var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(firstPlan);
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(firstPlan, new Pager(2, 10, "Name"));

        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 " +
                     "\r\nOrder By [Name] /* tail */", firstPlan.CommandText);
        Assert.Equal("Select Count(*) \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 /* tail */",
            countPlan.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 " +
                     "\r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only /* tail */",
            pagePlan.CommandText);
        Assert.Equal(new object[] { 1 }, Assert.IsType<SqlParam[]>(firstPlan.Parameters).Select(item => item.Value));
        Assert.Equal(new object[] { 1 }, Assert.IsType<SqlParam[]>(countPlan.Parameters).Select(item => item.Value));
        Assert.Equal(new object[] { 1, 10, 10 },
            Assert.IsType<SqlParam[]>(pagePlan.Parameters).Select(item => item.Value));
        Assert.Equal(1, filter.Calls);
    }

    /// <summary>自定义 SQL 尾部使模板走原生文本分支时，仍应冻结本次过滤参数且不修改草稿。</summary>
    [Fact]
    public void ToTemplate_WhenDynamicFilterAndCustomTailArePresent_ShouldFreezeSqlAndParameters()
    {
        var filter = new SequenceFilter();
        var builder = new TailSqlServerBuilder(CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s");

        var template = builder.ToTemplate();

        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 /* tail */",
            template.Sql);
        Assert.Equal(new object[] { 1 }, template.Parameters.Select(item => item.Value));
        Assert.Equal(1, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>动态过滤与 Take 同时使用时，派生页面应保留首次过滤值并替换原限制参数。</summary>
    [Fact]
    public void GetPlan_WhenTakeAndDynamicFilterAreCombined_ShouldReuseFilteredValueForPage()
    {
        var filter = new SequenceFilter();
        var builder = new SqliteBuilder(CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s").OrderBy("Name");
        builder.Take(5);
        var where = builder.WhereClause;
        var originalParameters = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);

        var firstPlan = query.GetPlan(forPaging: true);
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(firstPlan, new Pager(2, 10, "Name"));
        var firstParameters = Assert.IsType<SqlParam[]>(firstPlan.Parameters);
        var parameters = Assert.IsType<SqlParam[]>(pagePlan.Parameters);

        Assert.Equal("Select `Name` \r\nFrom `samples` As `s` \r\nWhere `s`.`TenantId`=@_p_1 " +
                     "\r\nOrder By `Name` \r\nLimit @_p_0 OFFSET @_p_2", firstPlan.CommandText);
        Assert.Equal("Select `Name` \r\nFrom `samples` As `s` \r\nWhere `s`.`TenantId`=@_p_1 " +
                     "\r\nOrder By `Name` \r\nLimit @_p_0 OFFSET @_p_2",
            pagePlan.CommandText);
        Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2" }, firstParameters.Select(item => item.Name));
        Assert.Equal(new object[] { 5, 1, 0 }, firstParameters.Select(item => item.Value));
        Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2" }, parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 10, 1, 10 }, parameters.Select(item => item.Value));
        Assert.Equal(1, filter.Calls);
        Assert.Same(where, builder.WhereClause);
        Assert.Equal(originalParameters, builder.GetParams().OrderBy(item => item.Key));
    }

    /// <summary>原生排序使查询计划回退时，分页仍应沿用首次捕获的动态过滤谓词和参数。</summary>
    [Fact]
    public void GetPlan_WhenAppendOrderByRequiresBuilderFallback_ShouldRetainFilteredSourceForPage()
    {
        var filter = new SequenceFilter();
        var builder = new SqlServerBuilder(CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s").AppendOrderBy("[Name]");
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);

        var firstPlan = query.GetPlan(forPaging: true);
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(firstPlan, new Pager(2, 10, "Name"));

        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 " +
                     "\r\nOrder By [Name]", firstPlan.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 " +
                     "\r\nOrder By [Name],[Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only",
            pagePlan.CommandText);
        Assert.Equal(new object[] { 1 }, Assert.IsType<SqlParam[]>(firstPlan.Parameters).Select(item => item.Value));
        Assert.Equal(new object[] { 1, 10, 10 },
            Assert.IsType<SqlParam[]>(pagePlan.Parameters).Select(item => item.Value));
        Assert.Equal(1, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>Right Join 使用 On，Full Join 使用来源预过滤；二者均隔离原始 Builder 状态。</summary>
    [Theory]
    [InlineData("Right")]
    [InlineData("Full")]
    public void OuterJoin_WhenDynamicFilterIsApplied_ShouldPreservePlacementAndInputState(string joinKind)
    {
        var filter = new SequenceFilter();
        var builder = new SqlServerBuilder(CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s");
        if (joinKind == "Right")
            builder.RightJoin<JoinedFilterRow>("j").On<FilterRow, JoinedFilterRow>((left, right) => left.Id == right.Id);
        else
            builder.FullJoin<JoinedFilterRow>("j").On<FilterRow, JoinedFilterRow>((left, right) => left.Id == right.Id);

        var select = builder.SelectClause;
        var from = builder.FromClause;
        var join = builder.JoinClause;
        var where = builder.WhereClause;
        var parameters = builder.GetParams().OrderBy(item => item.Key).ToArray();

        if (joinKind == "Right")
        {
            var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);

            Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nRight Join [joined_samples] As [j] " +
                         "On [s].[Id]=[j].[Id] And [s].[TenantId]=@_p_0", snapshot.Sql);
            Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nRight Join [joined_samples] As [j] " +
                         "On [s].[Id]=[j].[Id] And [s].[TenantId]=1", snapshot.DebugSql);
            Assert.Equal(new object[] { 1 }, snapshot.Parameters.Select(item => item.Value));
        }
        else
        {
            var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);
            const string expected = "Select [Name] \r\nFrom (Select * From [samples] As [s] Where [s].[TenantId]=@_p_0) As [s] \r\nFull Join [joined_samples] As [j] On [s].[Id]=[j].[Id]";
            Assert.Equal(expected, snapshot.Sql);
            Assert.Equal(expected.Replace("@_p_0", "1"), snapshot.DebugSql);
            Assert.Equal(new object[] { 1 }, snapshot.Parameters.Select(item => item.Value));
        }

        Assert.Equal(1, filter.Calls);
        Assert.Same(select, builder.SelectClause);
        Assert.Same(from, builder.FromClause);
        Assert.Same(join, builder.JoinClause);
        Assert.Same(where, builder.WhereClause);
        Assert.Equal(parameters, builder.GetParams().OrderBy(item => item.Key));
    }

    /// <summary>各内置 Provider 应对同一过滤快照生成完整 SQL 和匹配的调试文本。</summary>
    [Theory]
    [InlineData("SqlServer", "Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0",
        "Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=1")]
    [InlineData("MySql", "Select `Name` \r\nFrom `samples` As `s` \r\nWhere `s`.`TenantId`=@_p_0",
        "Select `Name` \r\nFrom `samples` As `s` \r\nWhere `s`.`TenantId`=1")]
    [InlineData("PostgreSql", "Select \"Name\" \r\nFrom \"samples\" As \"s\" \r\nWhere \"s\".\"TenantId\"=@_p_0",
        "Select \"Name\" \r\nFrom \"samples\" As \"s\" \r\nWhere \"s\".\"TenantId\"=1")]
    [InlineData("Sqlite", "Select `Name` \r\nFrom `samples` As `s` \r\nWhere `s`.`TenantId`=@_p_0",
        "Select `Name` \r\nFrom `samples` As `s` \r\nWhere `s`.`TenantId`=1")]
    public void DefaultProvider_WhenDynamicFilterIsApplied_ShouldRenderFullSqlAndDebugSql(
        string provider, string expectedSql, string expectedDebugSql)
    {
        var filter = new SequenceFilter();
        var services = CreateServices(filter);
        SqlBuilderBase builder = provider switch
        {
            "SqlServer" => new SqlServerBuilder(services),
            "MySql" => new MySqlBuilder(services),
            "PostgreSql" => new PostgreSqlBuilder(services),
            "Sqlite" => new SqliteBuilder(services),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
        builder.Select("Name").From<FilterRow>("s");

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);

        Assert.Equal(expectedSql, snapshot.Sql);
        Assert.Equal(expectedDebugSql, snapshot.DebugSql);
        Assert.Equal(1, Assert.Single(snapshot.Parameters).Value);
        Assert.Equal(1, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>重复生成调试 SQL 应每次使用新的过滤值，且不把谓词写回可编辑 Builder。</summary>
    [Fact]
    public void ToDebugSql_Repeatedly_ShouldUseFreshFilterValueWithoutAccumulatingPredicates()
    {
        var filter = new SequenceFilter();
        var builder = CreateFilteredBuilder(filter);

        var first = builder.ToDebugSql();
        var second = builder.ToDebugSql();

        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=1", first);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=2", second);
        Assert.Equal(2, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>测试 Builder 的克隆工厂应延续共享服务、参数值和克隆计数器。</summary>
    [Fact]
    public void Clone_ShouldPreserveServicesParametersAndSharedCounter()
    {
        var calls = new BuilderCalls();
        var services = new SqlBuilderServices();
        var builder = new CountedSqlServerBuilder(calls, services);
        builder.Select("Name").From("samples").Where("Id", 7);
        calls.CloneCount = 0;

        var clone = Assert.IsType<CountedSqlServerBuilder>(builder.Clone());
        var parameters = clone.GetParams();
        _ = clone.Clone();

        Assert.Same(services, clone.SharedServices);
        Assert.Equal(new object[] { 7 }, parameters.OrderBy(item => item.Key).Select(item => item.Value));
        Assert.Equal(2, calls.CloneCount);
    }

    /// <summary>过滤器提交谓词后失败时应保留输出前缀与草稿，成功重试只包含新谓词。</summary>
    [Fact]
    public void AppendTo_WhenFilterThrowsAfterAddingPredicate_ShouldPreservePrefixAndAllowRetry()
    {
        var filter = new ThrowAfterPredicateFilter();
        var builder = CreateFilteredBuilder(filter);
        var select = builder.SelectClause;
        var from = builder.FromClause;
        var join = builder.JoinClause;
        var where = builder.WhereClause;
        var groupBy = builder.GroupByClause;
        var orderBy = builder.OrderByClause;
        var parameters = builder.GetParams().OrderBy(item => item.Key).ToArray();
        var output = new StringBuilder("prefix");

        var error = Assert.Throws<InvalidOperationException>(() => builder.AppendTo(output));

        Assert.Equal("filter-failure", error.Message);
        Assert.Equal("prefix", output.ToString());
        Assert.Equal(parameters, builder.GetParams().OrderBy(item => item.Key));
        Assert.Same(select, builder.SelectClause);
        Assert.Same(from, builder.FromClause);
        Assert.Same(join, builder.JoinClause);
        Assert.Same(where, builder.WhereClause);
        Assert.Same(groupBy, builder.GroupByClause);
        Assert.Same(orderBy, builder.OrderByClause);

        var retry = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder, includeDebugSql: true);

        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0", retry.Sql);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=2", retry.DebugSql);
        Assert.Equal(2, Assert.Single(retry.Parameters).Value);
        Assert.Equal(2, filter.Calls);
        Assert.Empty(builder.GetParams());
    }

    private static CountedSqlServerBuilder CreateFilteredBuilder(ISqlFilter filter)
    {
        var builder = new CountedSqlServerBuilder(new BuilderCalls(), CreateServices(filter));
        builder.Select("Name").From<FilterRow>("s");
        return builder;
    }

    private static SqlBuilderServices CreateServices(params ISqlFilter[] filters) => new(
        filters: filters,
        options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities
            {
                Pagination = SqlQueryCapabilityState.Supported
            }
        });

    [Table("samples")]
    private sealed class FilterRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [Table("joined_samples")]
    private sealed class JoinedFilterRow
    {
        public int Id { get; set; }
    }

    private sealed class SequenceFilter : ISqlFilter
    {
        internal int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(FilterRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.TenantId)), ++Calls);
        }
    }

    private sealed class ThrowAfterPredicateFilter : ISqlFilter
    {
        internal int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(FilterRow));
            var value = ++Calls;
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.TenantId)), value);
            if (value == 1)
                throw new InvalidOperationException("filter-failure");
        }
    }

    private sealed class BuilderCalls
    {
        internal int CloneCount { get; set; }
    }

    private sealed class CountedSqlServerBuilder : SqlServerBuilder
    {
        private readonly BuilderCalls _calls;

        internal SqlBuilderServices SharedServices => Services;

        internal CountedSqlServerBuilder(BuilderCalls calls, SqlBuilderServices services,
            IParameterManager parameterManager = null)
            : base(services, parameterManager) => _calls = calls;

        protected override bool SupportsMutationCheckpoint => true;

        public override ISqlBuilder Clone()
        {
            _calls.CloneCount++;
            return base.Clone();
        }

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new CountedSqlServerBuilder(_calls, Services, parameterManager);
    }

    private sealed class TailSqlServerBuilder : SqlServerBuilder
    {
        internal TailSqlServerBuilder(SqlBuilderServices services, IParameterManager parameterManager = null)
            : base(services, parameterManager) { }

        protected override bool SupportsMutationCheckpoint => true;

        public override string ToSql() => base.ToSql() + " /* tail */";

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new TailSqlServerBuilder(Services, parameterManager);
    }
}
