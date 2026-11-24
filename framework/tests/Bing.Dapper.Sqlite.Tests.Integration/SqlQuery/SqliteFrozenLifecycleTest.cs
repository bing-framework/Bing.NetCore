using System.Threading;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>查询冻结后，预览与克隆都使用同一份冻结结构。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteFrozenLifecycleTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqliteFrozenLifecycleTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>首次执行后外部 Builder 的变更不影响预览和新建的可修改克隆。</summary>
    [Fact]
    public async Task FirstTerminal_ShouldFreezePreviewAndCloneSource()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2)");

        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var builder = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 1, Operator.GreaterEqual).OrderBy("Name");
        var query = new Bing.Data.Sql.SqlQuery(executor, builder);

        const string frozenSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0 \r\nOrder By `Name`";
        Assert.Equal(new[] { "one", "two" }, query.ToList<string>());
        builder.Where("Name", "missing");

        Assert.Equal(frozenSql, query.ToSql());
        var sourcePlan = query.GetPlan();
        Assert.Equal(frozenSql, sourcePlan.CommandText);
        Assert.Equal(new object[] { 1 }, Values(sourcePlan));

        var clone = query.Clone();
        clone.GetBuilder().Where("Name", "one");
        const string cloneSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0 And `Name`=@_p_1 \r\nOrder By `Name`";
        var clonePlan = clone.GetPlan();
        Assert.Equal(cloneSql, clonePlan.CommandText);
        Assert.Equal(new object[] { 1, "one" }, Values(clonePlan));
        Assert.Equal(new[] { "one" }, executor.ToList<string>(clonePlan, null));

        Assert.Equal(frozenSql, query.ToSql());
        Assert.Equal(frozenSql, query.GetPlan().CommandText);
        Assert.Equal(new object[] { 1 }, Values(query.GetPlan()));
    }

    /// <summary>预取消只冻结查询结构，外部草稿后续变化不能进入正常终结或克隆。</summary>
    [Fact]
    public async Task CancelledFirstTerminal_ShouldFreezeSourceForLaterTerminalAndClone()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1)");

        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var builder = new SqliteBuilder().Select("Name").From("samples").Where("Amount", 1);
        var query = new Bing.Data.Sql.SqlQuery(executor, builder);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var cancelledPlan = query.GetPlan(cancellation.Token);
        Assert.False(cancelledPlan.IsBuilderPlan);
        Assert.Null(cancelledPlan.CommandText);
        Assert.Null(cancelledPlan.Parameters);
        builder.Where("Name", "missing");

        const string frozenSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`=@_p_0";
        var plan = query.GetPlan();
        Assert.Equal(frozenSql, query.ToSql());
        Assert.Equal(frozenSql, plan.CommandText);
        Assert.Equal(new object[] { 1 }, Values(plan));
        Assert.Equal(new[] { "one" }, query.ToList<string>());

        var clone = query.Clone();
        clone.GetBuilder().Where("Name", "one");
        const string cloneSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`=@_p_0 And `Name`=@_p_1";
        var clonePlan = clone.GetPlan();
        Assert.Equal(cloneSql, clonePlan.CommandText);
        Assert.Equal(new object[] { 1, "one" }, Values(clonePlan));
        Assert.Equal(new[] { "one" }, executor.ToList<string>(clonePlan, null));
        Assert.Equal(frozenSql, query.GetPlan().CommandText);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<Bing.Data.Sql.Builders.Params.SqlParam[]>(plan.Parameters)
            .Select(parameter => parameter.Value).ToArray();
}
