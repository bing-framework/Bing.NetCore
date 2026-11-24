using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>公开 Builder 计划创建时立即冻结 SQL、参数和执行资源边界。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqlitePublicPlanFreezeTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqlitePublicPlanFreezeTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>默认 Create 重载在调用时冻结 Builder 与参数，且重复 Freeze 保持幂等。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateBuilderPlan_ShouldFreezeBeforeSyncOrAsyncExecution(bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 4), ('two', 5)");

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 4);
        var plan = SqlQueryPlan.Create(builder);
        const string expectedSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`=@_p_0";

        Assert.False(plan.IsBuilderPlan);
        Assert.Equal(expectedSql, plan.CommandText);
        Assert.Equal("Id", plan.SplitOn);
        Assert.Equal(new object[] { 4 }, ParameterValues(plan));
        Assert.Same(plan, plan.Freeze());

        var parameterName = Assert.Single(((ISqlCommonPartAccessor)builder).ParameterManager.GetParams().Keys);
        ((ISqlCommonPartAccessor)builder).ParameterManager.Add(parameterName, 5);
        builder.Where("Name", "missing");

        var rows = asynchronous
            ? await executor.ToListAsync<string>(plan, null, default)
            : executor.ToList<string>(plan, null);

        Assert.Equal(new[] { "one" }, rows);
        Assert.Equal(expectedSql, plan.CommandText);
        Assert.Equal(new object[] { 4 }, ParameterValues(plan));
    }

    /// <summary>带 splitOn 的公开重载从同一冻结来源派生 Count 和 Page。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateBuilderPlanWithSplitOn_ShouldIsolateCountAndPageFromBuilderChanges(bool asynchronous)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        var builder = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 0, Operator.Greater).OrderBy("Name");
        var plan = SqlQueryPlan.Create(builder, "Name");
        const string sourceSql =
            "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>@_p_0 \r\nOrder By `Name`";

        Assert.False(plan.IsBuilderPlan);
        Assert.Equal("Name", plan.SplitOn);
        Assert.Equal(sourceSql, plan.CommandText);
        Assert.Equal(new object[] { 0 }, ParameterValues(plan));

        var parameterName = Assert.Single(((ISqlCommonPartAccessor)builder).ParameterManager.GetParams().Keys);
        ((ISqlCommonPartAccessor)builder).ParameterManager.Add(parameterName, 10);
        builder.Where("Name", "missing");

        var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        var pager = new Pager(2, 1, "Name");
        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(plan, pager);
        Assert.Equal("Select Count(*) \r\nFrom `samples` \r\nWhere `Amount`>@_p_0", countPlan.CommandText);
        Assert.Equal(new object[] { 0 }, ParameterValues(countPlan));
        Assert.Equal(sourceSql + " \r\nLimit @_p_2 OFFSET @_p_1", pagePlan.CommandText);
        Assert.Equal(new object[] { 0, 1, 1 }, ParameterValues(pagePlan));

        var page = asynchronous
            ? await executor.ToPageAsync<string>(plan, pager, null, default)
            : executor.ToPage<string>(plan, pager, null);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "three" }, page.Data);
        Assert.Equal(sourceSql, plan.CommandText);
        Assert.Equal(new object[] { 0 }, ParameterValues(plan));
    }

    /// <summary>执行公开 Builder 计划会复用 writer 的工作单元事务并随其回滚。</summary>
    [Fact]
    public async Task PublicBuilderPlan_ShouldShareWriterUnitOfWorkAndRollback()
    {
        var registrations = new ServiceCollection();
        registrations.AddUnitOfWorkScopes();
        registrations.AddSqlCore();
        registrations.AddSqlDataSource("first", DatabaseType.Sqlite, _fixture.FirstConnectionString);
        registrations.AddSqliteProvider();
        using var services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = services.CreateScope();
        using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        using var writer = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create("first");
        using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create("first");
        await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('transactional', 8)");

        var plan = SqlQueryPlan.Create(new SqliteBuilder().Select("Name").From("samples")
            .Where("Name", "transactional"));
        Assert.False(plan.IsBuilderPlan);
        Assert.Equal("Select `Name` \r\nFrom `samples` \r\nWhere `Name`=@_p_0", plan.CommandText);
        Assert.Equal(new[] { "transactional" },
            await ((ISqlQueryPlanExecutor)query).ToListAsync<string>(plan, null, default));

        unitOfWork.Rollback();

        Assert.Empty(await _fixture.ReadNamesAsync());
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static object[] ParameterValues(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();
}
