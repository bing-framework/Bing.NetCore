using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>CTE 定义在创建时冻结外部查询片段。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteCteDefinitionSnapshotTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    public SqliteCteDefinitionSnapshotTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>共享 CTE 定义的两个 Builder 执行相同的创建时 SQL 和参数快照。</summary>
    [Fact]
    public async Task CteDefinition_WhenSourceChanges_ShouldExecuteOriginalSnapshotInIndependentBuilders()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        var source = new MutableFragment(
            "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@minimum",
            new SqlParam("@minimum", 2, DbType.Int32));
        var cte = new SqlCteDefinition("selected", source);
        var scope = new SqlQueryScope("shared").Add(cte);

        source.Sql = "Select `Name` From `samples` Where `Amount`>=@minimum And `Name`=@name";
        source.Parameters = new[]
        {
            new SqlParam("@minimum", 1, DbType.Int32),
            new SqlParam("@name", "missing", DbType.String)
        };

        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var first = CreateQuery(executor, cte);
        var second = CreateQuery(executor, cte);

        const string expectedSql = "With Recursive `selected` \r\n" +
            "As (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@minimum)\r\n" +
            "Select `Name` \r\nFrom `selected` \r\nOrder By `Name`";
        foreach (var query in new[] { first, second })
        {
            var plan = query.GetPlan();
            Assert.Equal(expectedSql, plan.CommandText);
            Assert.Equal(new[] { "@minimum" }, ParameterNames(plan));
            Assert.Equal(new object[] { 2 }, ParameterValues(plan));
            Assert.Equal(new[] { "three", "two" }, query.ToList<string>());
        }

        var renderedDefinition = scope.Ctes.Single().Render(scope.CreateChild("check"));
        Assert.Equal("selected AS (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@shared_check_minimum)",
            renderedDefinition.Sql);
        Assert.Equal(new[] { "shared_check_minimum" }, renderedDefinition.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 2 }, renderedDefinition.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>同一 CTE 定义渲染到不同子作用域后可独立作为查询片段执行。</summary>
    [Fact]
    public async Task CteDefinition_WhenRenderedToChildScopes_ShouldExecuteScopedFragmentSnapshot()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        var source = new MutableFragment(
            "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@minimum",
            new SqlParam("@minimum", 2, DbType.Int32));
        var cte = new SqlCteDefinition("selected", source);
        var scope = new SqlQueryScope("root").Add(cte);

        source.Sql = "Select `Name` From `samples` Where `Amount`>=@minimum And `Name`=@name";
        source.Parameters = new[]
        {
            new SqlParam("@minimum", 1, DbType.Int32),
            new SqlParam("@name", "missing", DbType.String)
        };

        var left = scope.Ctes.Single().Render(scope.CreateChild("left"));
        var right = scope.Ctes.Single().Render(scope.CreateChild("right"));

        Assert.Equal("selected AS (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@root_left_minimum)",
            left.Sql);
        Assert.Equal("selected AS (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@root_right_minimum)",
            right.Sql);
        Assert.Equal(new[] { "root_left_minimum" }, left.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new[] { "root_right_minimum" }, right.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 2 }, left.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 2 }, right.Parameters.Select(parameter => parameter.Value));

        var queryFragment = new SqlQueryFragment("With " + left.Sql + " Select `Name` From `selected` Order By `Name`",
            left.Parameters);
        var plan = SqlQueryPlan.Create(queryFragment);
        Assert.Equal("With selected AS (Select `Name` \r\nFrom `samples` \r\n" +
            "Where `Amount`>=@root_left_minimum) Select `Name` From `selected` Order By `Name`", plan.CommandText);
        Assert.Equal(new[] { "root_left_minimum" }, ParameterNames(plan));
        Assert.Equal(new object[] { 2 }, ParameterValues(plan));

        using var root = _fixture.CreateQuery();
        var result = ((ISqlQueryPlanExecutor)root).ToList<string>(plan, null);
        Assert.Equal(new[] { "three", "two" }, result);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static Bing.Data.Sql.SqlQuery CreateQuery(ISqlQueryPlanExecutor executor, SqlCteDefinition cte)
    {
        var builder = new SqliteBuilder().Select("Name").From(cte.Name).OrderBy("Name");
        builder.With(cte.Name, cte.Fragment);
        return new Bing.Data.Sql.SqlQuery(executor, builder);
    }

    private static string[] ParameterNames(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Name).ToArray();

    private static object[] ParameterValues(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();

    private sealed class MutableFragment : ISqlQueryFragment
    {
        internal MutableFragment(string sql, params SqlParam[] parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }

        internal string Sql { get; set; }
        internal SqlParam[] Parameters { get; set; }

        public SqlQueryFragmentSnapshot Freeze() => new SqlQueryFragment(Sql, Parameters).Freeze();
    }
}
