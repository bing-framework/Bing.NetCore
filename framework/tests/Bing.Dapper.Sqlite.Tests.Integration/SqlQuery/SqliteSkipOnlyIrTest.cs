using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证 Skip-only 冻结 IR 的 Count 和分页可在 SQLite 上真实执行。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSkipOnlyIrTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqliteSkipOnlyIrTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("simple")]
    [InlineData("group")]
    [InlineData("cte")]
    [InlineData("unionall")]
    public async Task SkipOnlyIr_ShouldExecuteCountAndPageAndKeepSource(string shape)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3), ('two', 4)");

        var builder = new SqliteBuilder().Select("Name");
        if (shape == "cte")
            builder.From("selected").With("selected", new SqliteBuilder().Select("Name").From("samples"));
        else
            builder.From("samples");
        if (shape == "group")
            builder.GroupBy("Name");
        if (shape == "unionall")
            builder.UnionAll(new SqliteBuilder().Select("Name").From("samples"));
        builder.OrderBy("Name").Skip(5);

        var source = SqlQueryPlan.Create(builder).Freeze();
        var sourceSql = ExpectedSourceSql(shape);
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(new object[] { 5 }, Values(source));

        var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        Assert.Equal(ExpectedCountSql(shape), countPlan.CommandText);
        Assert.Empty(Values(countPlan));

        var pager = new Pager(2, 2);
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(source, pager);
        Assert.Equal(sourceSql + " \r\nLimit @_p_1 OFFSET @_p_0", pagePlan.CommandText);
        Assert.Equal(new object[] { 2, 2 }, Values(pagePlan));

        var expectedCount = shape switch
        {
            "group" => 3,
            "unionall" => 8,
            _ => 4
        };
        var expectedRows = shape switch
        {
            "group" => new[] { "two" },
            "unionall" => new[] { "three", "three" },
            _ => new[] { "two", "two" }
        };
        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        Assert.Equal((long)expectedCount, executor.Scalar<long>(countPlan, null));

        var sync = executor.ToPage<string>(source, pager, null);
        Assert.Equal(expectedCount, sync.TotalCount);
        Assert.Equal(expectedRows, sync.Data);

        var asynchronous = await executor.ToPageAsync<string>(source, pager, null, default);
        Assert.Equal(expectedCount, asynchronous.TotalCount);
        Assert.Equal(expectedRows, asynchronous.Data);
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(new object[] { 5 }, Values(source));
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static string ExpectedSourceSql(string shape)
    {
        var prefix = shape == "cte"
            ? "With Recursive `selected` \r\nAs (Select `Name` \r\nFrom `samples`)\r\n"
            : string.Empty;
        var body = shape switch
        {
            "cte" => "Select `Name` \r\nFrom `selected`",
            "unionall" => "Select `Name` \r\nFrom `samples` \r\nUnion All \r\nSelect `Name` \r\nFrom `samples`",
            _ => "Select `Name` \r\nFrom `samples`"
        };
        var group = shape == "group" ? " \r\nGroup By `Name`" : string.Empty;
        return prefix + body + group + " \r\nOrder By `Name`";
    }

    private static string ExpectedCountSql(string shape)
    {
        if (shape == "simple")
            return "Select Count(*) \r\nFrom `samples`";
        if (shape == "cte")
            return "With Recursive `selected` \r\nAs (Select `Name` \r\nFrom `samples`)\r\nSelect Count(*) \r\nFrom `selected`";
        var body = shape == "group"
            ? "Select `Name` \r\nFrom `samples` \r\nGroup By `Name`"
            : "Select `Name` \r\nFrom `samples` \r\nUnion All \r\nSelect `Name` \r\nFrom `samples`";
        return $"Select Count(*) \r\nFrom ({body}) As `t`";
    }

    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();
}
