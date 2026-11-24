using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>Builder 模板转查询计划时保留冻结的 Count/Page 结构。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteQueryTemplateIrIntegrationTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqliteQueryTemplateIrIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>带作用域参数的 CTE 模板直接创建计划后，Count 和 Page 保持外层 CTE 结构。</summary>
    [Fact]
    public async Task ScopedCteTemplate_CreatePlan_ShouldDeriveAndExecuteCountAndPage()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        var cte = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 2, Operator.GreaterEqual).ToTemplate().AsCte("selected");
        var template = new SqliteBuilder().Select("Name").From("selected")
            .With(cte.Name, cte.Fragment).OrderBy("Name")
            .ToTemplate(new SqlQueryScope("request"));
        var plan = SqlQueryPlan.Create(template.CreateFragment());

        const string ctePrefix = "With Recursive `selected` \r\nAs (Select `Name` \r\n" +
            "From `samples` \r\nWhere `Amount`>=@request_p_0)\r\n";
        const string sourceSql = ctePrefix + "Select `Name` \r\nFrom `selected` \r\nOrder By `Name`";
        Assert.Equal(sourceSql, plan.CommandText);
        Assert.Equal(new[] { "request_p_0" }, Parameters(plan).Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 2 }, Values(plan));

        var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        Assert.Equal(ctePrefix + "Select Count(*) \r\nFrom `selected`", countPlan.CommandText);
        Assert.Equal(new[] { "request_p_0" }, Parameters(countPlan).Select(parameter => parameter.Name));

        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(2, 1));
        Assert.Equal(ctePrefix + "Select `Name` \r\nFrom `selected` \r\nOrder By `Name` " +
            "\r\nLimit @request_p_2 OFFSET @request_p_1", pagePlan.CommandText);
        Assert.Equal(new[] { "request_p_0", "@request_p_1", "@request_p_2" },
            Parameters(pagePlan).Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 2, 1, 1 }, Values(pagePlan));

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        Assert.Equal(2, executor.Scalar<int>(countPlan, null));
        Assert.Equal(new[] { "two" }, executor.ToList<string>(pagePlan, null));
        Assert.Equal(sourceSql, plan.CommandText);
        Assert.Equal(new object[] { 2 }, Values(plan));
    }

    /// <summary>同一 CTE 模板的两个消费者可用不同过滤值和页码独立派生与执行。</summary>
    [Fact]
    public async Task SharedTemplateConsumers_ShouldKeepDataAndPageParametersIndependent()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('alpha', 1), ('bravo', 2), ('charlie', 3), ('delta', 4)");

        var shared = new SqliteBuilder().Select("Name").From("samples").ToTemplate();
        var firstTemplate = CreateConsumer(shared, "first", "bravo");
        var secondTemplate = CreateConsumer(shared, "second", "charlie");
        var first = SqlQueryPlan.Create(firstTemplate.CreateFragment());
        var second = SqlQueryPlan.Create(secondTemplate.CreateFragment());

        const string firstCte = "With Recursive `selected` \r\nAs (Select `Name` \r\nFrom `samples`)\r\n";
        const string secondCte = firstCte;
        const string firstSql = firstCte + "Select `Name` \r\nFrom `selected` \r\n" +
            "Where `Name`>=@first_p_0 \r\nOrder By `Name`";
        const string secondSql = secondCte + "Select `Name` \r\nFrom `selected` \r\n" +
            "Where `Name`>=@second_p_0 \r\nOrder By `Name`";
        Assert.Equal(firstSql, first.CommandText);
        Assert.Equal(secondSql, second.CommandText);
        Assert.Equal(new object[] { "bravo" }, Values(first));
        Assert.Equal(new object[] { "charlie" }, Values(second));

        var firstCount = SqlBuilderRuntimeBridge.CreateCountPlan(first);
        var secondCount = SqlBuilderRuntimeBridge.CreateCountPlan(second);
        Assert.Equal(firstCte + "Select Count(*) \r\nFrom `selected` \r\nWhere `Name`>=@first_p_0",
            firstCount.CommandText);
        Assert.Equal(secondCte + "Select Count(*) \r\nFrom `selected` \r\nWhere `Name`>=@second_p_0",
            secondCount.CommandText);

        var firstPage = SqlBuilderRuntimeBridge.CreatePagePlan(first, new Pager(1, 1));
        var secondPage = SqlBuilderRuntimeBridge.CreatePagePlan(second, new Pager(2, 1));
        Assert.Equal(firstSql + " \r\nLimit @first_p_2 OFFSET @first_p_1", firstPage.CommandText);
        Assert.Equal(secondSql + " \r\nLimit @second_p_2 OFFSET @second_p_1", secondPage.CommandText);
        Assert.Equal(new object[] { "bravo", 0, 1 }, Values(firstPage));
        Assert.Equal(new object[] { "charlie", 1, 1 }, Values(secondPage));

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        Assert.Equal(3, executor.Scalar<int>(firstCount, null));
        Assert.Equal(2, executor.Scalar<int>(secondCount, null));
        Assert.Equal(new[] { "bravo" }, executor.ToList<string>(firstPage, null));
        Assert.Equal(new[] { "delta" }, executor.ToList<string>(secondPage, null));
        Assert.Equal(firstSql, first.CommandText);
        Assert.Equal(secondSql, second.CommandText);
        Assert.Equal(new object[] { "bravo" }, Values(first));
        Assert.Equal(new object[] { "charlie" }, Values(second));
        Assert.Empty(shared.Parameters);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static SqlQueryTemplate CreateConsumer(SqlQueryTemplate shared, string scope, string minimumName)
    {
        var cte = shared.AsCte("selected");
        return new SqliteBuilder().Select("Name").From("selected")
            .With(cte.Name, cte.Fragment).Where("Name", minimumName, Operator.GreaterEqual).OrderBy("Name")
            .ToTemplate(new SqlQueryScope(scope));
    }

    private static SqlParam[] Parameters(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters);

    private static object[] Values(SqlQueryPlan plan) => Parameters(plan).Select(parameter => parameter.Value).ToArray();
}
