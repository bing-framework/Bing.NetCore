using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>非 Union CTE 的冻结 Count/Page 在真实 SQLite 上执行。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteCteIrTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;
    public SqliteCteIrTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;
    public static IEnumerable<object[]> Cases()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var state in new[] { "unordered", "ordered", "paged" })
        foreach (var asynchronous in new[] { false, true })
            yield return new object[] { shape, state, asynchronous };
    }

    /// <summary>重复页执行与自动计数使用同一冻结结构，分组和去重按结果行数计数。</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FrozenCte_ShouldExecuteCountAndData(string shape, string state, bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3), ('two', 4)");
        var cte = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 0, Operator.Greater);
        var builder = new SqliteBuilder();
        if (shape == "aggregate") builder.CountAll("Total");
        else builder.Select("Name");
        builder.From("selected").With("selected", cte);
        if (shape == "group") builder.GroupBy("Name");
        if (shape == "distinct") builder.Distinct();
        var key = shape == "aggregate" ? "Total" : "Name";
        if (state != "unordered") builder.OrderBy(key);
        if (state == "paged") builder.Skip(3).Take(1);
        var source = SqlQueryPlan.Create(builder).Freeze();
        var originalSql = source.CommandText;
        Assert.Null(source.PlanningBuilder);

        var filter = state == "paged" ? 2 : 0;
        var offset = state == "paged" ? 0 : 1;
        var limit = state == "paged" ? 1 : 2;
        var prefix = $"With Recursive `selected` \r\nAs (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>@_p_{filter})\r\n";
        var select = shape == "aggregate" ? "Select Count(*) As `Total`" :
            shape == "distinct" ? "Select Distinct `Name`" : "Select `Name`";
        var group = shape == "group" ? " \r\nGroup By `Name`" : "";
        var body = select + " \r\nFrom `selected`" + group;
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 2, key + " Desc"));
        Assert.Equal(prefix + body + $" \r\nOrder By `{key}`" +
            (state == "unordered" ? " Desc" : "") + $" \r\nLimit @_p_{limit} OFFSET @_p_{offset}", page.CommandText);
        Assert.Equal(state == "paged" ? new object[] { 0, 2, 0 } : new object[] { 0, 0, 2 }, Values(page));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        Assert.Equal(prefix + (shape == "simple" ? "Select Count(*) \r\nFrom `selected`" :
            $"Select Count(*) \r\nFrom ({body}) As `t`"), count.CommandText);
        Assert.Equal(new object[] { 0 }, Values(count));

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        for (var number = 1; number <= 2; number++)
        {
            var pager = new Pager(number, 2, key + " Desc");
            if (shape == "aggregate")
            {
                var result = asynchronous ? await executor.ToPageAsync<long>(source, pager, null, default) :
                    executor.ToPage<long>(source, pager, null);
                Assert.Equal(1, result.TotalCount);
                Assert.Equal(number == 1 ? new long[] { 4 } : Array.Empty<long>(), result.Data);
            }
            else
            {
                var names = new[] { "one", "two", "three", "two" }.AsEnumerable();
                if (shape != "simple") names = names.Distinct();
                var ordered = state == "unordered" ? names.OrderByDescending(x => x, StringComparer.Ordinal) :
                    names.OrderBy(x => x, StringComparer.Ordinal);
                var result = asynchronous ? await executor.ToPageAsync<string>(source, pager, null, default) :
                    executor.ToPage<string>(source, pager, null);
                Assert.Equal(shape == "simple" ? 4 : 3, result.TotalCount);
                Assert.Equal(ordered.Skip((number - 1) * 2).Take(2), result.Data);
            }
        }
        Assert.Equal(originalSql, source.CommandText);
        Assert.Equal(state == "paged" ? new object[] { 3, 1, 0 } : new object[] { 0 }, Values(source));
    }
    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(p => p.Value).ToArray();
}
