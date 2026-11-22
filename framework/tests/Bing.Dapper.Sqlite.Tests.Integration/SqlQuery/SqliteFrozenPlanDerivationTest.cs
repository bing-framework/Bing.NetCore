using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证普通复杂查询冻结后，真实 Count 与数据页使用同一过滤快照。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteFrozenPlanDerivationTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>初始化 SQLite 冻结计划派生测试。</summary>
    public SqliteFrozenPlanDerivationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>Group/Distinct 的默认执行计划可重复分页，过滤器在一个冻结计划中仅计算一次。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DefaultComplexPlan_WhenPagedRepeatedly_ShouldKeepFrozenFilter(bool grouped, bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");
        using var query = _fixture.CreateQuery();
        var filter = new SequenceFilter();
        var builder = new SqliteBuilder(new SqlBuilderServices(filters: new[] { filter }))
            .Select("Name").From<FilterRow>("s");
        if (grouped)
            builder.GroupBy("Name");
        else
            ((ISqlQueryClauseAccessor)builder).SelectClause.Distinct();
        var executor = (ISqlQueryPlanExecutor)query;
        var description = new Bing.Data.Sql.SqlQuery(executor, builder);
        // 普通终结路径创建的计划也必须能在后续派生时保持原过滤值。
        var plan = description.GetPlan();

        for (var execution = 0; execution < 2; execution++)
        {
            var pager = new Pager(1, 2, "Name");
            var page = asynchronous
                ? await executor.ToPageAsync<string>(plan, pager, null, default)
                : executor.ToPage<string>(plan, pager, null);

            Assert.Equal(1, filter.Calls);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(new[] { "one" }, page.Data);
        }
    }

    /// <summary>简单分页计划派生 Count 时应移除原分页参数，且不改变原数据页。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultSimplePlan_WhenCountDerived_ShouldCountUnpagedRowsAndKeepPage(bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('zzz', 3)");

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("Name").From("samples").OrderBy("Name").Skip(1).Take(1);
        var plan = new Bing.Data.Sql.SqlQuery(executor, builder).GetPlan();
        var planParameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        var count = asynchronous
            ? await executor.ScalarAsync<int>(countPlan, null, default)
            : executor.Scalar<int>(countPlan, null);
        var data = asynchronous
            ? await executor.ToListAsync<string>(plan, null, default)
            : executor.ToList<string>(plan, null);

        Assert.Equal(3, count);
        Assert.Equal(new[] { "two" }, data);
        Assert.Equal("Select `Name` \r\nFrom `samples` \r\nOrder By `Name` \r\nLimit @_p_1 OFFSET @_p_0",
            plan.CommandText);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, planParameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 1, 1 }, planParameters.Select(parameter => parameter.Value));
        Assert.Equal("Select Count(*) \r\nFrom `samples`", countPlan.CommandText);
        Assert.Empty(Assert.IsType<SqlParam[]>(countPlan.Parameters));
    }

    /// <summary>纯 IR 冻结计划派生分页应重绑分页参数，且不改变源计划。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenSimplePlan_WhenPageDerivedFromIr_ShouldRebindPaginationWithoutChangingSource(
        bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("Name").From("samples").OrderBy("Name").Skip(1).Take(1);
        var plan = new Bing.Data.Sql.SqlQuery(executor, builder).GetPlan();
        const string originalSql =
            "Select `Name` \r\nFrom `samples` \r\nOrder By `Name` \r\nLimit @_p_1 OFFSET @_p_0";
        AssertSqlitePage(plan, originalSql, 1, 1);

        var pureIr = SqlQueryPlan.Create(plan.QueryIr, plan.SplitOn);
        pureIr.CopyContextFrom(plan, "Data");
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(pureIr, new Pager(2, 2));

        AssertSqlitePage(pagePlan, originalSql, 2, 2);
        AssertSqlitePage(plan, originalSql, 1, 1);

        var pageData = asynchronous
            ? await executor.ToListAsync<string>(pagePlan, null, default)
            : executor.ToList<string>(pagePlan, null);
        var originalData = asynchronous
            ? await executor.ToListAsync<string>(plan, null, default)
            : executor.ToList<string>(plan, null);

        Assert.Equal(new[] { "two" }, pageData);
        Assert.Equal(new[] { "three" }, originalData);
        AssertSqlitePage(plan, originalSql, 1, 1);
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    [Table("samples")]
    private sealed class FilterRow
    {
        public string Name { get; set; }
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
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.Name)),
                Calls == 1 ? "one" : Calls == 2 ? "two" : "three");
        }
    }

    private static void AssertSqlitePage(SqlQueryPlan plan, string expectedSql, int expectedOffset, int expectedLimit)
    {
        Assert.Equal(expectedSql, plan.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { expectedOffset, expectedLimit },
            new[] { parameters[0].Value, parameters[1].Value });
    }
}
