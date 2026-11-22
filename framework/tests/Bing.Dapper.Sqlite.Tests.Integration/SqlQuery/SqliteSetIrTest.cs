using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>集合查询冻结 IR 的真实计数与分页，包含重复数据和 CTE。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSetIrTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;
    public SqliteSetIrTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;
    public static IEnumerable<object[]> Cases()
    {
        foreach (var operation in new[] { "Union", "Union All", "Intersect", "Except" })
        foreach (var cte in new[] { false, true })
        foreach (var paged in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
            yield return new object[] { operation, cte, paged, asynchronous };
    }

    /// <summary>完整 SQL、参数、总数和页数据一致；源计划可重复使用。</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task FrozenSet_ShouldExecuteCountAndPage(string operation, bool cte, bool paged, bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one',1),('two',2),('three',3),('two',4)");
        var builder = new SqliteBuilder().Select("Name").From(cte ? "selected" : "samples").Where("Amount", 1, Operator.Greater);
        if (cte) builder.With("selected", new SqliteBuilder().Select("Name,Amount").From("samples"));
        var branch = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 3, Operator.LessEqual);
        switch (operation)
        {
            case "Union": builder.Union(branch); break;
            case "Union All": builder.UnionAll(branch); break;
            case "Intersect": builder.Intersect(branch); break;
            case "Except": builder.Except(branch); break;
        }
        if (paged) builder.OrderBy("Name").Skip(5).Take(1);
        var source = SqlQueryPlan.Create(builder).Freeze();
        var originalSql = source.CommandText;
        var originalValues = Values(source);
        Assert.Null(source.PlanningBuilder);
        var branchIndex = paged ? 3 : 1;
        var offset = paged ? 1 : 2;
        var limit = offset + 1;
        var prefix = cte ? "With Recursive `selected` \r\nAs (Select `Name`,`Amount` \r\nFrom `samples`)\r\n" : "";
        var body = $"Select `Name` \r\nFrom `{(cte ? "selected" : "samples")}` \r\nWhere `Amount`>@_p_0 \r\n{operation} \r\nSelect `Name` \r\nFrom `samples` \r\nWhere `Amount`<=@_p_{branchIndex}";
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 2, "Name"));
        Assert.Equal(prefix + body + $" \r\nOrder By `Name` \r\nLimit @_p_{limit} OFFSET @_p_{offset}", page.CommandText);
        Assert.Equal(paged ? new object[] { 1, 0, 2, 3 } : new object[] { 1, 3, 0, 2 }, Values(page));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        Assert.Equal(prefix + $"Select Count(*) \r\nFrom ({body}) As `t`", count.CommandText);
        Assert.Equal(new object[] { 1, 3 }, Values(count));
        var expected = operation switch
        {
            "Union" => new[] { "one", "three", "two" },
            "Union All" => new[] { "one", "three", "three", "two", "two", "two" },
            "Intersect" => new[] { "three", "two" },
            _ => Array.Empty<string>()
        };
        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        for (var number = 1; number <= 3; number++)
        {
            var pager = new Pager(number, 2, "Name");
            var result = asynchronous ? await executor.ToPageAsync<string>(source, pager, null, default) :
                executor.ToPage<string>(source, pager, null);
            Assert.Equal(expected.Length, result.TotalCount);
            Assert.Equal(expected.Skip((number - 1) * 2).Take(2), result.Data);
        }
        Assert.Equal(originalSql, source.CommandText);
        Assert.Equal(originalValues, Values(source));
    }
    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(p => p.Value).ToArray();
}
