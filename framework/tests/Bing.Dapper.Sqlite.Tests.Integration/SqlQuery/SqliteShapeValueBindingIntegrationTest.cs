using System.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>按槽位绑定模板参数的 SQLite 执行合同。</summary>
public sealed class SqliteShapeValueBindingIntegrationTest
{
    /// <summary>条件关闭后尾部槽位前移，重复渲染复用 SQL 并独立绑定值。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void RenderValues_WhenOptionalConditionChanges_ShouldExecuteSelectedLayout(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE items(id INTEGER, tenant INTEGER, name TEXT);" +
                "INSERT INTO items VALUES(1, 10, 'alice'), (2, 10, 'bob'), (3, 20, 'alice');";
            command.ExecuteNonQuery();
        }
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT id FROM items WHERE tenant=@tenant",
                new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "AND name=@name", new[] { new SqlParam("@name", "", DbType.String) }) },
            " ", new SqlQueryFragment("ORDER BY id LIMIT @take", new[] { new SqlParam("@take", 0, DbType.Int32) }));
        var shape = new SqlQueryShapeKey("items", "sqlite", "v1", "default", 1, "");

        var first = template.RenderValues(shape, new object[] { 10, "alice", 2 });
        var second = template.RenderValues(shape, new object[] { 20, "alice", 2 });
        var disabled = template.RenderValues(shape with { OptionalMask = 0 }, new object[] { 10, 2 });

        Assert.Equal("SELECT id FROM items WHERE tenant=@tenant AND name=@name ORDER BY id LIMIT @take", first.Sql);
        Assert.Same(first.Sql, second.Sql);
        Assert.Equal("SELECT id FROM items WHERE tenant=@tenant ORDER BY id LIMIT @take", disabled.Sql);
        Assert.Equal(new[] { 1 }, query.Sql(first.Sql, first.Parameters).ToList<int>());
        Assert.Equal(new[] { 3 }, query.Sql(second.Sql, second.Parameters).ToList<int>());
        Assert.Equal(new[] { 1, 2 }, query.Sql(disabled.Sql, disabled.Parameters).ToList<int>());
    }

    /// <summary>不同逻辑键的同名槽独立绑定，相同逻辑键的不同名称只占一个槽。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void RenderValues_WhenLogicalParametersRepeat_ShouldExecuteSharedAndIndependentSlots(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var template = SqlQueryShapeTemplate.CreateOrdered(
            new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 1, DbType.Int32) }).WithParameterKey("id", firstKey),
            new (SqlQueryFragment Fragment, int? OptionalBit)[]
            {
                (new SqlQueryFragment("UNION ALL SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) })
                    .WithParameterKey("id", secondKey), 0),
                (new SqlQueryFragment("UNION ALL SELECT @same", new[] { new SqlParam("@same", 2, DbType.Int32) })
                    .WithParameterKey("same", firstKey), null)
            });

        var fragment = template.RenderValues(1, new object[] { 7, 9 });

        Assert.Equal("SELECT @id UNION ALL SELECT @id_1 UNION ALL SELECT @id", fragment.Sql);
        Assert.Equal(new[] { firstKey, secondKey }, fragment.Slots);
        Assert.Equal(new[] { 7, 9, 7 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
    }

    /// <summary>二进制值保留快照，NULL 作为实际请求值传入现有参数绑定路径。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void RenderValues_WhenBinaryOrNullIsBound_ShouldExecuteRequestSnapshot(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT 7 WHERE @payload=X'010203' AND @name IS NULL",
            new[] { new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary), new SqlParam("@name", "default", DbType.String) }));
        var payload = new byte[] { 1, 2, 3 };
        var fragment = template.RenderValues(0, new object[] { payload, null });
        payload[0] = 9;

        Assert.Equal("SELECT 7 WHERE @payload=X'010203' AND @name IS NULL", fragment.Sql);
        Assert.Equal(new[] { 7 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
        var empty = template.RenderValues(0, new object[] { Array.Empty<byte>(), null });
        Assert.Equal("SELECT 7 WHERE @payload=X'010203' AND @name IS NULL", empty.Sql);
        Assert.Empty(query.Sql(empty.Sql, empty.Parameters).ToList<int>());
    }

    /// <summary>槽位集合参数通过生成式绑定器展开，输入后续变更不影响已捕获的快照。</summary>
    [Fact]
    public void RenderValues_WhenCollectionIsBound_ShouldExecuteWithGeneratedBinder()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT 7 WHERE 7 IN @ids AND @name IS NULL",
            new[] { new SqlParam("@ids", Array.Empty<int>()), new SqlParam("@name", "default", DbType.String) }));
        var ids = new[] { 7, 9 };
        var fragment = template.RenderValues(0, new object[] { ids, null });
        ids[0] = 100;
        using var command = connection.CreateCommand();
        command.CommandText = fragment.Sql;
        SqlGeneratedExecution.BindParameters(command, fragment.Parameters);

        Assert.Equal("SELECT 7 WHERE 7 IN (@ids0,@ids1) AND @name IS NULL", command.CommandText);
        Assert.Equal(7L, command.ExecuteScalar());
        var empty = template.RenderValues(0, new object[] { Array.Empty<int>(), null });
        using var emptyCommand = connection.CreateCommand();
        emptyCommand.CommandText = empty.Sql;
        SqlGeneratedExecution.BindParameters(emptyCommand, empty.Parameters);
        Assert.Equal("SELECT 7 WHERE 7 IN (SELECT 1 WHERE 1 = 0) AND @name IS NULL", emptyCommand.CommandText);
        Assert.Null(emptyCommand.ExecuteScalar());
    }

    /// <summary>槽位绑定只准备参数，查询仍可参与现有事务并遵守回滚。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void RenderValues_WhenTransactionIsShared_ShouldSeeAndRollbackWrite(SqlExecutionMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = "shape-values-" + Guid.NewGuid().ToString("N"),
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
            "SELECT id FROM items WHERE tenant=@tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }));
        using (var transaction = host.GetRequiredService<ISqlTransactionScopeFactory>().Begin())
        {
            using var writer = transaction.CreateExecutor();
            using var query = transaction.CreateQuery();
            writer.ExecuteSql("INSERT INTO items VALUES(1, 10)");
            var fragment = template.RenderValues(0, new object[] { 10 });

            Assert.Equal("SELECT id FROM items WHERE tenant=@tenant", fragment.Sql);
            Assert.Equal(new[] { 1 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
            transaction.Rollback();
        }
        using var verify = keeper.CreateCommand();
        verify.CommandText = "SELECT COUNT(*) FROM items";
        Assert.Equal(0L, verify.ExecuteScalar());
    }

    /// <summary>并发请求共享模板，各自持有查询、连接和参数快照。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public async Task RenderValues_WhenRequestsOverlap_ShouldExecuteIndependentValues(SqlExecutionMode mode)
    {
        using var host = CreateHost(mode);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) }));
        await Task.WhenAll(Enumerable.Range(1, 32).Select(id => Task.Run(async () =>
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
                { DatabaseType = DatabaseType.Sqlite, Connection = connection });
            var fragment = template.RenderValues(0, new object[] { id });
            Assert.Equal("SELECT @id", fragment.Sql);
            Assert.Equal(id, await query.Sql(fragment.Sql, fragment.Parameters).ScalarAsync<int>());
        })));
    }

    private static ServiceProvider CreateHost(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        return services.BuildServiceProvider();
    }
}
