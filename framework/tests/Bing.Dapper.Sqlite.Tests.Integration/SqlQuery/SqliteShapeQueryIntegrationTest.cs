using System.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>查询扩展直接执行形状模板的 SQLite 集成测试。</summary>
public sealed class SqliteShapeQueryIntegrationTest
{
    /// <summary>按掩码选择可选条件，并保留固定排序和分页尾部。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Sql_WhenOptionalConditionChanges_ShouldExecuteSelectedLayout(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE items(id INTEGER, tenant INTEGER, status TEXT);" +
                "INSERT INTO items VALUES(1, 10, 'paid'), (2, 10, 'new'), (3, 20, 'paid');";
            command.ExecuteNonQuery();
        }
        using var query = CreateQuery(host, connection);
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT id FROM items WHERE tenant=@tenant",
                new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "AND status=@status",
                new[] { new SqlParam("@status", "", DbType.String) }) },
            " ", new SqlQueryFragment("ORDER BY id LIMIT @take",
                new[] { new SqlParam("@take", 0, DbType.Int32) }));

        var enabled = query.Sql(template, 1, new object[] { 10, "paid", 2 });
        var disabled = query.Sql(template, 0, new object[] { 10, 2 });

        Assert.Equal("SELECT id FROM items WHERE tenant=@tenant AND status=@status ORDER BY id LIMIT @take",
            enabled.CommandText);
        Assert.Equal("SELECT id FROM items WHERE tenant=@tenant ORDER BY id LIMIT @take", disabled.CommandText);
        Assert.Equal(new[] { 1 }, enabled.ToList<int>());
        Assert.Equal(new[] { 1, 2 }, disabled.ToList<int>());
    }

    /// <summary>结构键和作用域同时参与渲染并可由原生查询入口执行。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Sql_WhenShapeAndScopeAreProvided_ShouldExecuteScopedParameters(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE items(id INTEGER, tenant INTEGER);" +
                "INSERT INTO items VALUES(1, 10), (2, 20);";
            command.ExecuteNonQuery();
        }
        using var query = CreateQuery(host, connection);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT id FROM items WHERE tenant=@tenant",
            new[] { new SqlParam("@tenant", 0, DbType.Int32) }));
        var shape = new SqlQueryShapeKey("items", "sqlite", "mapping-v1", "route-a", 0, "");

        var rendered = query.Sql(template, shape, new object[] { 20 }, new SqlQueryScope("child"));

        Assert.Equal("SELECT id FROM items WHERE tenant=@child_tenant", rendered.CommandText);
        Assert.Equal(new[] { 2 }, rendered.ToList<int>());
    }

    /// <summary>查询描述保留二进制及 NULL 参数快照，公开参数读取也返回独立副本。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Sql_WhenBinaryAndNullParametersAreMutated_ShouldExecuteOriginalSnapshots(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = CreateQuery(host, connection);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT 7 WHERE @payload=X'010203' AND @name IS NULL",
            new[]
            {
                new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary),
                new SqlParam("@name", "default", DbType.String)
            }));
        var payload = new byte[] { 1, 2, 3 };

        var rendered = query.Sql(template, 0, new object[] { payload, null });
        payload[0] = 9;
        var exposedParameters = Assert.IsType<SqlParam[]>(rendered.Parameters);
        Assert.IsType<byte[]>(exposedParameters[0].Value)[0] = 8;
        exposedParameters[1].Value = "changed";

        Assert.Equal("SELECT 7 WHERE @payload=X'010203' AND @name IS NULL", rendered.CommandText);
        Assert.Equal(new[] { 7 }, rendered.ToList<int>());
    }

    /// <summary>相同逻辑键共享参数，不同逻辑键即使同名也分别绑定。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Sql_WhenLogicalKeysShareAndSeparateSlots_ShouldExecuteBoundValues(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = CreateQuery(host, connection);
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var template = SqlQueryShapeTemplate.CreateOrdered(
            new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 1, DbType.Int32) })
                .WithParameterKey("id", firstKey),
            new (SqlQueryFragment Fragment, int? OptionalBit)[]
            {
                (new SqlQueryFragment("UNION ALL SELECT @id",
                    new[] { new SqlParam("@id", 0, DbType.Int32) }).WithParameterKey("id", secondKey), 0),
                (new SqlQueryFragment("UNION ALL SELECT @same",
                    new[] { new SqlParam("@same", 2, DbType.Int32) }).WithParameterKey("same", firstKey), null)
            });

        var rendered = query.Sql(template, 1, new object[] { 7, 9 });

        Assert.Equal("SELECT @id UNION ALL SELECT @id_1 UNION ALL SELECT @id", rendered.CommandText);
        Assert.Equal(new[] { 7, 9, 7 }, rendered.ToList<int>());
    }

    /// <summary>模板查询通过事务查询共享事务写入，并遵守回滚结果。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Sql_WhenUsingSharedTransaction_ShouldReadWriteAndRollback(SqlExecutionMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = "shape-query-" + Guid.NewGuid().ToString("N"),
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        using (var command = keeper.CreateCommand())
        {
            command.CommandText = "CREATE TABLE items(id INTEGER, tenant INTEGER);";
            command.ExecuteNonQuery();
        }
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
        services.AddSqliteProvider();
        using var host = services.BuildServiceProvider();
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT id FROM items WHERE tenant=@tenant",
            new[] { new SqlParam("@tenant", 0, DbType.Int32) }));

        using (var transaction = host.GetRequiredService<ISqlTransactionScopeFactory>().Begin())
        {
            using var writer = transaction.CreateExecutor();
            using var query = transaction.CreateQuery();
            writer.ExecuteSql("INSERT INTO items VALUES(1, 10)");
            var rendered = query.Sql(template, 0, new object[] { 10 });

            Assert.Equal("SELECT id FROM items WHERE tenant=@tenant", rendered.CommandText);
            Assert.Equal(new[] { 1 }, rendered.ToList<int>());
            transaction.Rollback();
        }

        using var verify = keeper.CreateCommand();
        verify.CommandText = "SELECT COUNT(*) FROM items";
        Assert.Equal(0L, verify.ExecuteScalar());
    }

    /// <summary>并发查询各用独立连接，重复使用模板时参数值仍相互隔离。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public async Task Sql_WhenConcurrentQueriesShareTemplate_ShouldKeepValuesIndependent(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) }));

        await Task.WhenAll(Enumerable.Range(1, 32).Select(id => Task.Run(async () =>
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            using var query = CreateQuery(host, connection);
            var rendered = query.Sql(template, 0, new object[] { id });

            Assert.Equal("SELECT @id", rendered.CommandText);
            Assert.Equal(id, await rendered.ScalarAsync<int>());
        })));
    }

    /// <summary>Compatible 原生文本查询继续通过现有参数路径展开集合和空集合。</summary>
    [Fact]
    public void Sql_WhenCollectionIsBoundInCompatibleMode_ShouldExecuteCollectionAndEmptyCollection()
    {
        using var host = CreateHost(SqlExecutionMode.Compatible);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = CreateQuery(host, connection);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT 7 WHERE 7 IN @ids", new[] { new SqlParam("@ids", Array.Empty<int>()) }));

        var populated = query.Sql(template, 0, new object[] { new[] { 7, 9 } });
        var empty = query.Sql(template, 0, new object[] { Array.Empty<int>() });

        Assert.Equal("SELECT 7 WHERE 7 IN @ids", populated.CommandText);
        Assert.Equal("SELECT 7 WHERE 7 IN @ids", empty.CommandText);
        Assert.Equal(new[] { 7 }, populated.ToList<int>());
        Assert.Empty(empty.ToList<int>());
    }

    private static ServiceProvider CreateHost(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        return services.BuildServiceProvider();
    }

    private static SqliteSqlQuery CreateQuery(ServiceProvider host, SqliteConnection connection) =>
        new(host, new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite,
            Connection = connection
        });
}
