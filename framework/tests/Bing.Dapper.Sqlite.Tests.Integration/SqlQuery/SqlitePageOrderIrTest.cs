using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>分页排序 IR 在真实 SQLite 上执行 Count 和数据页。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqlitePageOrderIrTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;
    public SqlitePageOrderIrTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var state in new[] { "unordered", "ordered", "paged" })
        foreach (var grouped in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
            yield return new object[] { state, grouped, asynchronous };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RequestedOrder_ShouldExecuteCountAndDataAndKeepSource(string state, bool grouped, bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");
        var builder = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 0, Operator.Greater);
        if (grouped) builder.GroupBy("Name");
        if (state != "unordered") builder.OrderBy("Name");
        if (state == "paged") builder.Skip(5).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var group = grouped ? " \r\nGroup By `Name`" : "";
        var body = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>@_p_0" + group;
        var sourceSql = body + (state == "unordered" ? "" : " \r\nOrder By `Name`") +
            (state == "paged" ? " \r\nLimit @_p_2 OFFSET @_p_1" : "");
        var expected = body + " \r\nOrder By `Name`" + (state == "unordered" ? " Desc" : "") +
            " \r\nLimit @_p_2 OFFSET @_p_1";
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 1, "Name Desc,Name"));
        Assert.Equal(expected, page.CommandText);
        Assert.Equal(new object[] { 0, 0, 1 }, Values(page));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        Assert.Equal(grouped ? $"Select Count(*) \r\nFrom ({body}) As `t`" :
            "Select Count(*) \r\nFrom `samples` \r\nWhere `Amount`>@_p_0", count.CommandText);
        Assert.Equal(new object[] { 0 }, Values(count));

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        for (var number = 1; number <= 2; number++)
        {
            var pager = new Pager(number, 1, "Name Desc,Name");
            var result = asynchronous ? await executor.ToPageAsync<string>(source, pager, null, default) :
                executor.ToPage<string>(source, pager, null);
            Assert.Equal(3, result.TotalCount);
            Assert.Equal(new[] { state == "unordered" ? number == 1 ? "two" : "three" : number == 1 ? "one" : "three" }, result.Data);
        }
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(state == "paged" ? new object[] { 0, 5, 2 } : new object[] { 0 }, Values(source));
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(p => p.Value).ToArray();
}
