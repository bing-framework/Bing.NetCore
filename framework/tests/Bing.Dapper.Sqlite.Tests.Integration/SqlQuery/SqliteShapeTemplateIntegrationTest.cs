using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>查询形状模板与 SQLite 查询执行的集成契约。</summary>
public sealed class SqliteShapeTemplateIntegrationTest
{
    /// <summary>直接替换派生表参数后，旧逻辑键不能改变 SQLite 实际绑定值。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void BuilderTemplate_WhenParameterIsReplacedDirectly_ShouldExecuteReplacement(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 7, DbType.Int32) })
            .WithParameterKey("id", key);
        var builder = new SqliteBuilder().Select("*").From(fragment, "items");
        builder.ParameterManager.Add("id", 19);

        var shape = new SqlQueryShapeTemplate(builder.ToTemplate().CreateFragment());
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 23 });

        Assert.Equal("Select * \r\nFrom (SELECT @id AS value) As `items`", rendered.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id") }, rendered.Slots);
        Assert.Equal(new[] { 19 }, query.Sql(rendered.Sql, rendered.Parameters).ToList<int>());
    }

    /// <summary>带不同逻辑键的嵌套 Builder 在实际查询中分别接收请求值。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void BuilderTemplate_WhenNestedBuildersHaveDifferentKeys_ShouldExecuteIndependentBindings(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var first = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 0, DbType.Int32) })
            .WithParameterKey("id", firstKey);
        var second = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 0, DbType.Int32) })
            .WithParameterKey("id", secondKey);
        var firstBuilder = new SqliteBuilder().Select("*").From(first, "a");
        var secondBuilder = new SqliteBuilder().Select("*").From(second, "b");
        var builder = new SqliteBuilder().Select("*").From("items").With("items", firstBuilder).UnionAll(secondBuilder);
        var shape = new SqlQueryShapeTemplate(builder.ToTemplate().CreateFragment());

        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object>
            { [firstKey] = 7, [secondKey] = 9 });

        Assert.Equal("With Recursive `items` \r\nAs (Select * \r\nFrom (SELECT @id AS value) As `a`)\r\nSelect * \r\nFrom `items` \r\nUnion All \r\nSelect * \r\nFrom (SELECT @_p_0 AS value) As `b`", rendered.Sql);
        Assert.Equal(new[] { firstKey, secondKey }, rendered.Slots);
        Assert.Equal(new[] { 7, 9 }, query.Sql(rendered.Sql, rendered.Parameters).ToList<int>());
    }

    /// <summary>Builder 冻结后的不同逻辑键在 SQLite 实际执行时分别绑定。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void BuilderTemplate_WhenFragmentKeysDiffer_ShouldExecuteIndependentBindings(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var first = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 0, DbType.Int32) })
            .WithParameterKey("id", firstKey);
        var second = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 0, DbType.Int32) })
            .WithParameterKey("id", secondKey);
        var builder = new SqliteBuilder().Select("*").From("items").With("items", first).UnionAll(second);
        var shape = new SqlQueryShapeTemplate(builder.ToTemplate().CreateFragment());
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object>
            { [firstKey] = 7, [secondKey] = 9 });

        Assert.Equal("With Recursive `items` \r\nAs (SELECT @id AS value)\r\nSelect * \r\nFrom `items` \r\nUnion All \r\nSELECT @_p_0 AS value", rendered.Sql);
        Assert.Equal(new[] { 7, 9 }, query.Sql(rendered.Sql, rendered.Parameters).ToList<int>());
    }

    /// <summary>不同物理名称共享同一逻辑键后，SQLite 两个分支使用相同请求值。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void BuilderTemplate_WhenFragmentKeyIsShared_ShouldExecuteOneBinding(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var key = new SqlParameterKey("request", "id");
        var first = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 1, DbType.Int32) })
            .WithParameterKey("id", key);
        var second = new SqlQueryFragment("SELECT @other AS value", new[] { new SqlParam("other", 2, DbType.Int32) })
            .WithParameterKey("other", key);
        var builder = new SqliteBuilder().Select("*").From("items").With("items", first).UnionAll(second);
        var shape = new SqlQueryShapeTemplate(builder.ToTemplate().CreateFragment());

        Assert.Throws<InvalidOperationException>(() => shape.RenderWithKeys(0, null));
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 7 });
        Assert.Equal("With Recursive `items` \r\nAs (SELECT @id AS value)\r\nSelect * \r\nFrom `items` \r\nUnion All \r\nSELECT @id AS value", rendered.Sql);
        Assert.Single(rendered.Parameters);
        Assert.Equal(new[] { 7, 7 }, query.Sql(rendered.Sql, rendered.Parameters).ToList<int>());
    }

    /// <summary>SQLite 的 CTE 和 Union 均可直接组合冻结片段并稳定执行。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void BuilderFragment_WhenUsedInCteAndUnion_ShouldExecuteTwice(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var cte = new SqlQueryFragment("SELECT @ID AS value", new[] { new SqlParam("id", 7, DbType.Int32) });
        var union = new SqlQueryFragment("SELECT :ID AS value", new[] { new SqlParam("id", 8, DbType.Int32) });
        var builder = new SqliteBuilder().Select("*").From("items").With("items", cte).UnionAll(union);
        const string expected = "With Recursive `items` \r\nAs (SELECT @id AS value)\r\nSelect * \r\nFrom `items` \r\nUnion All \r\nSELECT @_p_0 AS value";

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(new[] { 7, 8 }, query.Sql(builder.ToSql(), builder.GetSqlParams().Values).ToList<int>());
        Assert.Equal(new[] { 7, 8 }, query.Sql(builder.ToSql(), builder.GetSqlParams().Values).ToList<int>());
    }

    /// <summary>片段作为派生表时，大小写、前缀和重名处理应与 SQLite 的实际参数绑定一致。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void BuilderFragment_WhenParametersCollide_ShouldExecuteWithIndependentValues(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var fragment = new SqlQueryFragment("SELECT @ID AS value UNION ALL SELECT :ID",
            new[] { new SqlParam("@id", 7, DbType.Int32), new SqlParam(":id", 9, DbType.Int32) });
        var builder = new SqliteBuilder();
        builder.AddParam("id", 100);
        builder.Select("*").From(fragment, "items");

        Assert.Equal("Select * \r\nFrom (SELECT @_p_0 AS value UNION ALL SELECT @_p_1) As `items`", builder.ToSql());
        Assert.Equal(new[] { 7, 9 }, query.Sql(builder.ToSql(), builder.GetSqlParams().Values).ToList<int>());
    }

    /// <summary>不同拼写的同一逻辑参数在 SQLite 中只绑定一次。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void RenderWithKeys_WhenNamesDiffer_ShouldExecuteWithOneSharedValue(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var key = new SqlParameterKey("request", "id");
        var first = new SqlQueryFragment("SELECT :ID", new[] { new SqlParam("id", 0, DbType.Int32) })
            .WithParameterKey("id", key);
        var second = new SqlQueryFragment("UNION ALL SELECT :other", new[] { new SqlParam(":other", 2, DbType.Int32) })
            .WithParameterKey("other", key);
        var template = new SqlQueryShapeTemplate(first, new[] { new SqlQueryOptionalFragment(0, second) });

        Assert.Throws<InvalidOperationException>(() => template.RenderWithKeys(1, null));
        var fragment = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [key] = 7 });

        Assert.Equal("SELECT :id UNION ALL SELECT :id", fragment.Sql);
        Assert.Equal(new[] { 7, 7 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
    }

    /// <summary>相同名称的不同逻辑键可独立绑定，并在 SQLite 中返回各自的值。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void RenderWithKeys_WhenNamesMatch_ShouldExecuteWithIndependentValues(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var first = new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) })
            .WithParameterKey("@id", firstKey);
        var second = new SqlQueryFragment("UNION ALL SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) })
            .WithParameterKey("@id", secondKey);
        var template = new SqlQueryShapeTemplate(first, new[] { new SqlQueryOptionalFragment(0, second) });

        var fragment = template.RenderWithKeys(1,
            new Dictionary<SqlParameterKey, object> { [firstKey] = 7, [secondKey] = 9 });

        Assert.Equal("SELECT @id UNION ALL SELECT @id_1", fragment.Sql);
        Assert.Equal(new[] { 7, 9 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
    }

    /// <summary>共享参数的拼写差异必须与 SQLite 实际绑定名称保持一致。</summary>
    [Theory]
    [InlineData("@ID", SqlExecutionMode.Compatible)]
    [InlineData(":id", SqlExecutionMode.Compatible)]
    [InlineData("@ID", SqlExecutionMode.GeneratedOnly)]
    [InlineData(":id", SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenSharedSlotSpellingDiffers_ShouldExecuteWithOneValue(string repeatedName, SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE " + repeatedName + " = @id",
                new[] { new SqlParam(repeatedName, 1, DbType.Int32) }) });

        var fragment = template.Render(1, new Dictionary<string, object> { ["id"] = 7 });

        Assert.Equal(new[] { 7 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
        Assert.Equal("SELECT @id WHERE @id = @id", fragment.Sql);
    }

    /// <summary>首次声明不带前缀时，仍按其 SQL 标记绑定共享值。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenFirstSharedParameterHasNoPrefix_ShouldExecuteWithSqlToken(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT :id", new[] { new SqlParam("id", 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE :ID = :id",
                new[] { new SqlParam(":ID", 1, DbType.Int32) }) });

        var fragment = template.Render(1, new Dictionary<string, object> { ["id"] = 7 });

        Assert.Equal("SELECT :id WHERE :id = :id", fragment.Sql);
        Assert.Equal(new[] { 7 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
    }

    /// <summary>共享槽默认值冲突时，提供请求值后应能实际执行并绑定一次。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenSharedDefaultsConflict_ShouldExecuteAfterExplicitBinding(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @value", new[] { new SqlParam("@value", 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE @value > 0", new[] { new SqlParam("@value", 2, DbType.Int32) }) });

        Assert.Throws<InvalidOperationException>(() => template.Render(1));
        var fragment = template.Render(1, new Dictionary<string, object> { ["value"] = 7 });

        Assert.Equal("SELECT @value WHERE @value > 0", fragment.Sql);
        Assert.Single(fragment.Parameters);
        Assert.Equal(new[] { 7 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
    }

    /// <summary>组合片段应隔离同名参数，并保留 SQL 字符串中的原始文本。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Compose_WhenSqlContainsParameterLikeLiteral_ShouldExecuteWithIsolatedValues(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        const string sql = "SELECT CASE WHEN '@id' = char(64) || 'id' THEN @id ELSE -1 END /* @id */";
        var first = new SqlQueryFragment(sql, new[] { new SqlParam("@id", 7, DbType.Int32) });
        var second = new SqlQueryFragment(sql, new[] { new SqlParam("@id", 9, DbType.Int32) });

        var composed = SqlQueryFragment.Compose(" UNION ALL ", first, second);

        Assert.Equal("SELECT CASE WHEN '@id' = char(64) || 'id' THEN @fragment_0_id ELSE -1 END /* @id */" +
            " UNION ALL SELECT CASE WHEN '@id' = char(64) || 'id' THEN @fragment_1_id ELSE -1 END /* @id */", composed.Sql);
        Assert.Equal(new[] { 7, 9 }, query.Sql(composed.Sql, composed.Parameters).ToList<int>());
    }

    /// <summary>规范化后重名的参数在作用域模板执行时仍分别绑定。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenNormalizedNamesCollide_ShouldExecuteWithDistinctValues(SqlExecutionMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        using var host = services.BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "SELECT CASE WHEN @id=7 AND @_id=9 AND @id_1=11 THEN 1 ELSE 0 END",
            new[] { new SqlParam("@id", 0), new SqlParam("@_id", 0), new SqlParam("@id_1", 0) }));

        var fragment = template.Render(0,
            new Dictionary<string, object> { ["id"] = 7, ["_id"] = 9, ["id_1"] = 11 }, new SqlQueryScope("root"));

        Assert.Equal("SELECT CASE WHEN @root_id=7 AND @root_id_1=9 AND @root_id_1_1=11 THEN 1 ELSE 0 END", fragment.Sql);
        Assert.Equal(new[] { 1 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
    }

    /// <summary>同一缓存形状对不同请求绑定独立参数，可选条件关闭时不影响后续查询。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedPreferred)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenExecutedBySqlQuery_ShouldBindCurrentRequest(SqlExecutionMode mode)
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
            command.CommandText = "CREATE TABLE users (id INTEGER, tenant_id INTEGER, name TEXT); " +
                "INSERT INTO users VALUES (1, 10, 'alice'), (2, 10, 'bob'), (3, 20, 'alice');";
            command.ExecuteNonQuery();
        }

        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT id FROM users WHERE tenant_id=@tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "AND name=@name", new[] { new SqlParam("@name", "", DbType.String) }) });
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "default", 1, string.Empty);
        var first = template.Render(shape, new Dictionary<string, object> { ["tenant"] = 10, ["name"] = "alice" }, null);
        var second = template.Render(shape, new Dictionary<string, object> { ["tenant"] = 20, ["name"] = "alice" }, null);
        var unfiltered = template.Render(shape with { OptionalMask = 0 }, new Dictionary<string, object> { ["tenant"] = 10 }, null);

        Assert.Equal("SELECT id FROM users WHERE tenant_id=@tenant AND name=@name", first.Sql);
        Assert.Same(first.Sql, second.Sql);
        Assert.Equal("SELECT id FROM users WHERE tenant_id=@tenant", unfiltered.Sql);
        Assert.Equal(new[] { 1 }, query.Sql(first.Sql, first.Parameters).ToList<int>());
        Assert.Equal(new[] { 3 }, query.Sql(second.Sql, second.Parameters).ToList<int>());
        Assert.Equal(new[] { 1, 2 }, query.Sql(unfiltered.Sql, unfiltered.Parameters).ToList<int>());
    }

    /// <summary>使用共享事务执行模板时可以读取未提交写入，并保持回滚语义。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenExecutedInSharedTransaction_ShouldSeeAndRollbackWrite(SqlExecutionMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = "shape-" + Guid.NewGuid().ToString("N"),
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        using var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        using (var command = keeper.CreateCommand())
        {
            command.CommandText = "CREATE TABLE users (id INTEGER, tenant_id INTEGER);";
            command.ExecuteNonQuery();
        }
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
        services.AddSqliteProvider();
        using var host = services.BuildServiceProvider();
        using (var transaction = host.GetRequiredService<ISqlTransactionScopeFactory>().Begin())
        {
            using var writer = transaction.CreateExecutor();
            using var query = transaction.CreateQuery();
            writer.ExecuteSql("INSERT INTO users VALUES (1, 10)");
            var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
                "SELECT id FROM users WHERE tenant_id=@tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }));
            var fragment = template.Render(0, new Dictionary<string, object> { ["tenant"] = 10 });

            Assert.Equal("SELECT id FROM users WHERE tenant_id=@tenant", fragment.Sql);
            Assert.Equal(new[] { 1 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
            transaction.Rollback();
        }
        using var verify = keeper.CreateCommand();
        verify.CommandText = "SELECT COUNT(*) FROM users";
        Assert.Equal(0L, verify.ExecuteScalar());
    }

    /// <summary>二进制参数应按渲染时的内容执行，后续修改输入数组不影响查询。</summary>
    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public void Render_WhenBinaryInputChanges_ShouldExecuteWithSnapshot(SqlExecutionMode mode)
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
            command.CommandText = "CREATE TABLE payloads (id INTEGER, value BLOB); INSERT INTO payloads VALUES (1, X'010203');";
            command.ExecuteNonQuery();
        }

        using var query = new SqliteSqlQuery(host, new SqlOptions<SqliteSqlQuery>
            { DatabaseType = DatabaseType.Sqlite, Connection = connection });
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT id FROM payloads WHERE value=@payload",
            new[] { new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary) }));
        var payload = new byte[] { 1, 2, 3 };
        var fragment = template.Render(0, new Dictionary<string, object> { ["payload"] = payload });
        payload[0] = 9;

        Assert.Equal("SELECT id FROM payloads WHERE value=@payload", fragment.Sql);
        Assert.Equal(new[] { 1 }, query.Sql(fragment.Sql, fragment.Parameters).ToList<int>());
    }
}
