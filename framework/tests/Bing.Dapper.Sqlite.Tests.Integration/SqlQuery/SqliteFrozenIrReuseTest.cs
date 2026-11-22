using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>静态查询复用结构后，每次执行仍读取数据库，并独立派生 Count 和数据页。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteFrozenIrReuseTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqliteFrozenIrReuseTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>预览缓存命中和未命中都复用 IR，但不缓存数据或执行状态。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedTerminal_ShouldReuseStructureAndReadFreshRows(bool previewed)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2)");
        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var builder = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 1, Operator.GreaterEqual)
            .OrderBy("Name");
        var query = new Bing.Data.Sql.SqlQuery(executor, builder);
        const string sql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0 \r\nOrder By `Name`";
        if (previewed) Assert.Equal(sql, query.ToSql());
        var first = query.GetPlan();
        Assert.Equal(new[] { "one", "two" }, executor.ToList<string>(first, null));
        Assert.NotNull(first.ExecutionId);

        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('three', 3)");
        // 结构冻结后，原 Builder 的变化不能污染后续执行。
        builder.Where("Name", "missing");
        Assert.Equal(new[] { "one", "three", "two" }, await query.ToListAsync<string>());
        var next = query.GetPlan(true);
        Assert.Same(first.QueryIr, next.QueryIr);
        Assert.NotSame(first, next);
        Assert.Null(next.ExecutionId);
        Assert.Equal(first.QueryContextId, next.QueryContextId);
        Assert.Equal(sql, next.CommandText);
        Assert.Equal(new object[] { 1 }, Values(next));
        Assert.Equal("Select Count(*) \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0",
            SqlBuilderRuntimeBridge.CreateCountPlan(next).CommandText);
        Assert.Equal(sql + " \r\nLimit @_p_2 OFFSET @_p_1",
            SqlBuilderRuntimeBridge.CreatePagePlan(next, new Pager(2, 1)).CommandText);

        var page = query.ToPage<string>(new Pager(2, 1));
        var asyncPage = await query.ToPageAsync<string>(new Pager(3, 1));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, asyncPage.TotalCount);
        Assert.Equal(new[] { "three" }, page.Data);
        Assert.Equal(new[] { "two" }, asyncPage.Data);
        Assert.Same(first.QueryIr, query.GetPlan().QueryIr);
        Assert.Equal(sql, first.CommandText);
        Assert.Equal(new object[] { 1 }, Values(first));
    }

    /// <summary>只有 CTE 子查询携带过滤器时，每次终结也重新取值，同次 Count/Data 共用该值。</summary>
    [Fact]
    public async Task NestedDynamicFilter_ShouldReadCurrentRowsAndKeepPageSnapshot()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2)");
        using var root = _fixture.CreateQuery();
        var filter = new AlternatingFilter();
        var child = new SqliteBuilder(new SqlBuilderServices(filters: new[] { filter }))
            .Select("Name").From<FilterRow>("s");
        var query = new Bing.Data.Sql.SqlQuery((ISqlQueryPlanExecutor)root,
            new SqliteBuilder().Select("Name").From("recent").With("recent", child).OrderBy("Name"));
        const string sql = "With Recursive `recent` \r\nAs (Select `Name` \r\nFrom `samples` As `s` \r\nWhere `s`.`Name`=@_p_0)\r\nSelect `Name` \r\nFrom `recent` \r\nOrder By `Name`";
        Assert.Equal(new[] { "one" }, query.ToList<string>());
        Assert.Equal(new[] { "two" }, await query.ToListAsync<string>());
        var page = query.ToPage<string>(new Pager(1, 1));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(new[] { "one" }, page.Data);
        Assert.Equal(3, filter.Calls);
        var next = query.GetPlan();
        Assert.Equal(sql, next.CommandText);
        Assert.Equal(new object[] { "two" }, Values(next));
        Assert.Equal(4, filter.Calls);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();

    [Table("samples")]
    private sealed class FilterRow { public string Name { get; set; } }

    private sealed class AlternatingFilter : ISqlFilter
    {
        internal int Calls { get; private set; }
        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(FilterRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.Name)),
                ++Calls % 2 == 1 ? "one" : "two");
        }
    }
}
