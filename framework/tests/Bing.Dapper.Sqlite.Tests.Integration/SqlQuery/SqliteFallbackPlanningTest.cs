using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>回退规划来源在真实 SQLite 中连续分页和计数的执行验证。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteFallbackPlanningTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;
    public SqliteFallbackPlanningTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>从页面重新派生仍以未分页查询计数，保留外部 SQL 扩展并隔离原页参数。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DerivedFallbackPage_ShouldExecuteCountAndRepage(bool grouped)
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync("Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3), ('two', 4)");
        var builder = new TailBuilder().Select("Name").From("samples").Where("Name", "two").OrderBy("Name");
        if (grouped)
            builder.GroupBy("Name");
        var source = SqlQueryPlan.Create(builder).Freeze();
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 1, "Name"));
        var next = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(1, 1, "Name"));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(next);
        var body = "Select `Name` \r\nFrom `samples` \r\nWhere `Name`=@_p_0" +
            (grouped ? " \r\nGroup By `Name`" : string.Empty);
        var sourceSql = body + " \r\nOrder By `Name` /* extension */";
        var pageSql = body + " \r\nOrder By `Name` \r\nLimit @_p_2 OFFSET @_p_1 /* extension */";
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(pageSql, page.CommandText);
        Assert.Equal(pageSql, next.CommandText);
        Assert.Equal(grouped ? "Select Count(*) \r\nFrom (" + body + " /* extension */) As `t` /* extension */"
            : "Select Count(*) \r\nFrom `samples` \r\nWhere `Name`=@_p_0 /* extension */", count.CommandText);
        Assert.Equal(new object[] { "two", 1, 1 }, Values(page));
        Assert.Equal(new object[] { "two", 0, 1 }, Values(next));
        Assert.Equal(new object[] { "two" }, Values(count));

        using var query = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)query;
        Assert.Equal(grouped ? 1 : 2, executor.Scalar<long>(count, null));
        Assert.Equal(grouped ? Array.Empty<string>() : new[] { "two" }, executor.ToList<string>(page, null));
        Assert.Equal(new[] { "two" }, await executor.ToListAsync<string>(next, null, default));
        var sync = executor.ToPage<string>(page, new Pager(1, 1, "Name"), null);
        var asynchronous = await executor.ToPageAsync<string>(page, new Pager(1, 1, "Name"), null, default);
        Assert.Equal(grouped ? 1 : 2, sync.TotalCount);
        Assert.Equal(sync.TotalCount, asynchronous.TotalCount);
        Assert.Equal(new[] { "two" }, sync.Data);
        Assert.Equal(sync.Data, asynchronous.Data);
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(new object[] { "two" }, Values(source));
        Assert.Equal(new object[] { "two", 1, 1 }, Values(page));
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();

    private sealed class TailBuilder : SqliteBuilder
    {
        internal TailBuilder(IParameterManager parameters = null) : base(parameterManager: parameters) { }
        public override string ToSql() => base.ToSql() + " /* extension */";
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) => new TailBuilder(parameterManager);
    }
}
