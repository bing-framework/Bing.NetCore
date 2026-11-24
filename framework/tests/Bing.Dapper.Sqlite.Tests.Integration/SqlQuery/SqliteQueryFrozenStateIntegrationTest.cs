using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>冻结计划的组合状态和分页绑定在实际 SQLite 执行中保持独立。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteQueryFrozenStateIntegrationTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqliteQueryFrozenStateIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>清理和重新组合草稿会更新只读视图，已捕获模板仍执行原先的 SQL 和参数。</summary>
    [Fact]
    public async Task ClearedComposition_ShouldUpdateDraftViewAndKeepFrozenTemplateIndependent()
    {
        await SeedAsync();
        var builder = new SqliteBuilder();
        builder.Select("Name").From("selected").OrderBy("Name")
            .With("selected", SelectNames(2, Operator.GreaterEqual));
        var view = builder.CteItems;
        var frozen = SqlQueryPlan.Create(builder.ToTemplate(new SqlQueryScope("request")));

        builder.ClearCte().With("selected", SelectNames(1, Operator.LessEqual));
        Assert.Same(view, builder.CteItems);
        Assert.Single(view);
        var replacement = SqlQueryPlan.Create(builder.ToTemplate());

        const string prefix = "With Recursive `selected` \r\nAs (Select `Name` \r\nFrom `samples` \r\n";
        const string tail = ")\r\nSelect `Name` \r\nFrom `selected` \r\nOrder By `Name`";
        Assert.Equal(prefix + "Where `Amount`>=@request_p_0" + tail, frozen.CommandText);
        Assert.Equal(prefix + "Where `Amount`<=@_p_0" + tail, replacement.CommandText);
        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        Assert.Equal(new[] { "bravo", "charlie", "delta" }, executor.ToList<string>(frozen, null));
        Assert.Equal(new[] { "alpha" }, await executor.ToListAsync<string>(replacement, null, CancellationToken.None));
        Assert.Equal(new object[] { 2 }, Values(frozen));
        Assert.Equal(new object[] { 1 }, Values(replacement));
    }

    /// <summary>作用域模板中的既有 Skip 参数在重复分页时更新，Count 只保留业务条件参数。</summary>
    [Fact]
    public async Task ScopedSkipTemplate_ShouldExecuteRepeatedPagesAndCountWithIndependentParameters()
    {
        await SeedAsync();
        var source = SqlQueryPlan.Create(SelectNames(1, Operator.GreaterEqual).OrderBy("Name").Skip(99)
            .ToTemplate(new SqlQueryScope("request")));
        var first = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 2));
        var second = SqlBuilderRuntimeBridge.CreatePagePlan(first, new Pager(1, 1));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(second);
        const string body = "From `samples` \r\nWhere `Amount`>=@request_p_0";
        const string pageSql = "Select `Name` \r\n" + body + " \r\nOrder By `Name` \r\n" +
            "Limit @request_p_2 OFFSET @request_p_1";
        Assert.Equal(pageSql, first.CommandText);
        Assert.Equal(pageSql, second.CommandText);
        Assert.Equal("Select Count(*) \r\n" + body, count.CommandText);
        Assert.Equal(new object[] { 1, 2, 2 }, Values(first));
        Assert.Equal(new object[] { 1, 0, 1 }, Values(second));
        Assert.Equal(new object[] { 1 }, Values(count));

        Assert.IsType<SqlParam[]>(first.Parameters)[0].Value = 999;
        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        Assert.Equal(new[] { "charlie", "delta" }, executor.ToList<string>(first, null));
        Assert.Equal(new[] { "alpha" }, await executor.ToListAsync<string>(second, null, CancellationToken.None));
        Assert.Equal(4, executor.Scalar<int>(count, null));
        Assert.Equal(new object[] { 1, 99 }, Values(source));
        Assert.Equal(new object[] { 1, 2, 2 }, Values(first));
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedAsync()
    {
        using var writer = _fixture.CreateExecutor();
        await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values " +
            "('alpha', 1), ('bravo', 2), ('charlie', 3), ('delta', 4)");
    }

    private static ISqlBuilder SelectNames(int amount, Operator operation) =>
        new SqliteBuilder().Select("Name").From("samples").Where("Amount", amount, operation);

    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();
}
