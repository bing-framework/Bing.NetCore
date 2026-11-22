using System.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>查询形状模板固定后缀与可选条件的 SQLite 集成契约。</summary>
public sealed class SqliteShapeSuffixIntegrationTest
{
    /// <summary>可选条件掩码变化时仍组合固定排序和分页后缀，并按请求参数执行。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenOptionalConditionsAndSuffixAreCombined_ShouldExecuteEachShape(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE items (id INTEGER, enabled INTEGER, category TEXT); " +
                "INSERT INTO items VALUES (1, 1, 'A'), (2, 1, 'B'), (3, 1, 'A'), (4, 0, 'A'), (5, 1, 'A');";
            command.ExecuteNonQuery();
        }

        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var required = new SqlQueryFragment("SELECT id FROM items WHERE enabled=@enabled",
            new[] { new SqlParam("@enabled", 0, DbType.Int32) });
        var optional = new[]
        {
            new SqlQueryOptionalFragment(0, "AND category=@category",
                new[] { new SqlParam("@category", "", DbType.String) }),
            new SqlQueryOptionalFragment(1, "AND id>=@minimumId",
                new[] { new SqlParam("@minimumId", 0, DbType.Int32) })
        };
        var suffix = new SqlQueryFragment("ORDER BY id LIMIT @take",
            new[] { new SqlParam("@take", 0, DbType.Int32) });
        var template = new SqlQueryShapeTemplate(required, optional, " ", suffix);

        var noOptionalConditions = template.Render(0, new Dictionary<string, object>
            { ["enabled"] = 1, ["take"] = 2 });
        var categoryCondition = template.Render(1, new Dictionary<string, object>
            { ["enabled"] = 1, ["category"] = "A", ["take"] = 10 });
        var minimumIdCondition = template.Render(2, new Dictionary<string, object>
            { ["enabled"] = 1, ["minimumId"] = 3, ["take"] = 10 });
        var bothConditions = template.Render(3, new Dictionary<string, object>
            { ["enabled"] = 1, ["category"] = "A", ["minimumId"] = 2, ["take"] = 1 });
        var sameShapeDifferentValues = template.Render(3, new Dictionary<string, object>
            { ["enabled"] = 1, ["category"] = "A", ["minimumId"] = 4, ["take"] = 10 });

        Assert.Equal("SELECT id FROM items WHERE enabled=@enabled ORDER BY id LIMIT @take", noOptionalConditions.Sql);
        Assert.Equal("SELECT id FROM items WHERE enabled=@enabled AND category=@category ORDER BY id LIMIT @take", categoryCondition.Sql);
        Assert.Equal("SELECT id FROM items WHERE enabled=@enabled AND id>=@minimumId ORDER BY id LIMIT @take", minimumIdCondition.Sql);
        Assert.Equal("SELECT id FROM items WHERE enabled=@enabled AND category=@category AND id>=@minimumId ORDER BY id LIMIT @take", bothConditions.Sql);
        Assert.Equal(bothConditions.Sql, sameShapeDifferentValues.Sql);

        Assert.Equal(new[] { 1, 2 }, query.Sql(noOptionalConditions.Sql, noOptionalConditions.Parameters).ToList<int>());
        Assert.Equal(new[] { 1, 3, 5 }, query.Sql(categoryCondition.Sql, categoryCondition.Parameters).ToList<int>());
        Assert.Equal(new[] { 3, 5 }, query.Sql(minimumIdCondition.Sql, minimumIdCondition.Parameters).ToList<int>());
        Assert.Equal(new[] { 3 }, query.Sql(bothConditions.Sql, bothConditions.Parameters).ToList<int>());
        Assert.Equal(new[] { 5 }, query.Sql(sameShapeDifferentValues.Sql, sameShapeDifferentValues.Parameters).ToList<int>());
    }
}
