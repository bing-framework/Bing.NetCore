using System.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>有序查询形状在 SQLite 执行链中的集成契约。</summary>
public sealed class SqliteShapeOrderedIntegrationTest
{
    /// <summary>可选 WHERE 和 HAVING 位于固定 GROUP BY 两侧时，各种形状都可执行。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenWhereAndHavingSurroundGroupBy_ShouldExecuteEveryShape(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = CreateConnection();
        using var query = CreateQuery(host, connection);
        var template = SqlQueryShapeTemplate.CreateOrdered(
            new SqlQueryFragment("SELECT category FROM items WHERE enabled=@enabled",
                new[] { new SqlParam("@enabled", 0, DbType.Int32) }),
            new (SqlQueryFragment Fragment, int? OptionalBit)[]
            {
                (new SqlQueryFragment("AND id>=@minimumId", new[] { new SqlParam("@minimumId", 0, DbType.Int32) }), 1),
                (new SqlQueryFragment("GROUP BY category"), null),
                (new SqlQueryFragment("HAVING COUNT(*)>=@minimumCount", new[] { new SqlParam("@minimumCount", 0, DbType.Int32) }), 0),
                (new SqlQueryFragment("ORDER BY category"), null)
            });
        var values = new Dictionary<string, object> { ["enabled"] = 1, ["minimumId"] = 3, ["minimumCount"] = 2 };
        var expectedSql = new[]
        {
            "SELECT category FROM items WHERE enabled=@enabled GROUP BY category ORDER BY category",
            "SELECT category FROM items WHERE enabled=@enabled GROUP BY category HAVING COUNT(*)>=@minimumCount ORDER BY category",
            "SELECT category FROM items WHERE enabled=@enabled AND id>=@minimumId GROUP BY category ORDER BY category",
            "SELECT category FROM items WHERE enabled=@enabled AND id>=@minimumId GROUP BY category HAVING COUNT(*)>=@minimumCount ORDER BY category"
        };
        var expected = new[] { new[] { "A", "B" }, new[] { "A" }, new[] { "A" }, new[] { "A" } };

        for (var mask = 0; mask < expectedSql.Length; mask++)
        {
            var result = template.Render(mask, values);
            Assert.Equal(expectedSql[mask], result.Sql);
            Assert.Equal(expected[mask], query.Sql(result.Sql, result.Parameters).ToList<string>());
        }

        var nextRequest = template.Render(3, new Dictionary<string, object>
            { ["enabled"] = 1, ["minimumId"] = 4, ["minimumCount"] = 2 });
        Assert.Equal(expectedSql[3], nextRequest.Sql);
        Assert.Empty(query.Sql(nextRequest.Sql, nextRequest.Parameters).ToList<string>());
    }

    /// <summary>同一条件位可同时控制 JOIN 和后续 WHERE 片段。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenOneBitControlsJoinAndWhere_ShouldExecuteBothShapes(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = CreateConnection();
        using var query = CreateQuery(host, connection);
        var template = SqlQueryShapeTemplate.CreateOrdered(new SqlQueryFragment("SELECT i.id FROM items i"),
            new (SqlQueryFragment Fragment, int? OptionalBit)[]
            {
                (new SqlQueryFragment("JOIN categories c ON c.name=i.category"), 0),
                (new SqlQueryFragment("WHERE i.enabled=@enabled", new[] { new SqlParam("@enabled", 0, DbType.Int32) }), null),
                (new SqlQueryFragment("AND c.visible=@visible", new[] { new SqlParam("@visible", 0, DbType.Int32) }), 0),
                (new SqlQueryFragment("ORDER BY i.id"), null)
            });
        var values = new Dictionary<string, object> { ["enabled"] = 1, ["visible"] = 1 };

        var withoutJoin = template.Render(0, values);
        var withJoin = template.Render(1, values);

        Assert.Equal("SELECT i.id FROM items i WHERE i.enabled=@enabled ORDER BY i.id", withoutJoin.Sql);
        Assert.Equal("SELECT i.id FROM items i JOIN categories c ON c.name=i.category WHERE i.enabled=@enabled AND c.visible=@visible ORDER BY i.id", withJoin.Sql);
        Assert.Equal(new[] { 1, 2, 3, 5 }, query.Sql(withoutJoin.Sql, withoutJoin.Parameters).ToList<int>());
        Assert.Equal(new[] { 1, 3, 5 }, query.Sql(withJoin.Sql, withJoin.Parameters).ToList<int>());
    }

    private static ServiceProvider CreateHost(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        return services.BuildServiceProvider();
    }

    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE items (id INTEGER, enabled INTEGER, category TEXT); " +
            "INSERT INTO items VALUES (1, 1, 'A'), (2, 1, 'B'), (3, 1, 'A'), (4, 0, 'A'), (5, 1, 'A'); " +
            "CREATE TABLE categories (name TEXT, visible INTEGER); INSERT INTO categories VALUES ('A', 1), ('B', 0);";
        command.ExecuteNonQuery();
        return connection;
    }

    private static SqliteSqlQuery CreateQuery(ServiceProvider host, SqliteConnection connection) =>
        new(host, new SqlOptions<SqliteSqlQuery> { DatabaseType = DatabaseType.Sqlite, Connection = connection });
}
