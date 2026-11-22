using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>未分页的冻结 IR 通过真实 SQLite 同步和异步执行 Count/Page。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteUnpagedIrPageTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;
    public SqliteUnpagedIrPageTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>普通、分组与去重的有序计划直接派生分页，源计划和再次分页互不污染。</summary>
    [Theory]
    [InlineData("simple", false)]
    [InlineData("simple", true)]
    [InlineData("group", false)]
    [InlineData("group", true)]
    [InlineData("distinct", false)]
    [InlineData("distinct", true)]
    public async Task OrderedIr_WhenPaged_ShouldExecuteCountAndDataAndKeepSource(string shape, bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");
        var builder = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 0, Operator.Greater);
        if (shape == "group") builder.GroupBy("Name");
        if (shape == "distinct") builder.Distinct();
        builder.OrderBy("Name");
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var select = shape == "distinct" ? "Select Distinct `Name`" : "Select `Name`";
        const string body = "From `samples` \r\nWhere `Amount`>@_p_0";
        var group = shape == "group" ? " \r\nGroup By `Name`" : "";
        var originalSql = $"{select} \r\n{body}{group} \r\nOrder By `Name`";
        Assert.Equal(originalSql, source.CommandText);
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 1));
        Assert.Equal(originalSql + " \r\nLimit @_p_2 OFFSET @_p_1", pagePlan.CommandText);
        Assert.Equal(new object[] { 0, 1, 1 }, Parameters(pagePlan));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(pagePlan);
        Assert.Equal(shape == "simple" ? $"Select Count(*) \r\n{body}" :
            $"Select Count(*) \r\nFrom ({select} \r\n{body}{group}) As `t`", count.CommandText);
        Assert.Equal(new object[] { 0 }, Parameters(count));
        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        for (var number = 2; number <= 3; number++)
        {
            var pager = new Pager(number, 1);
            var result = asynchronous
                ? await executor.ToPageAsync<string>(source, pager, null, default)
                : executor.ToPage<string>(source, pager, null);
            Assert.Equal(3, result.TotalCount);
            Assert.Equal(new[] { number == 2 ? "three" : "two" }, result.Data);
        }
        Assert.Equal(originalSql, source.CommandText);
        Assert.Equal(new object[] { 0 }, Parameters(source));
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static object[] Parameters(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();
}
