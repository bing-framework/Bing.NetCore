using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>通过真实 SQLite 验证编译期生成键 SQL 的读取、回填和事务边界。</summary>
public sealed class SqliteGeneratedKeyInsertSqlTest
{
    [Fact]
    public async Task InsertWithKeys_ShouldUseGeneratedSqlAndCommitNullableRows()
    {
        using var host = new Host();
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var first = new Row { Name = null };
        var second = new Row { Name = "second" };
        var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<Row, int>(new[]
        {
            new InsertRow<Row>("b", first), new InsertRow<Row>("a", second)
        });
        Assert.Equal(WriteCompletionState.Committed, result.Completion);
        Assert.True(first.Id > 0);
        Assert.True(second.Id > first.Id);
        Assert.Equal(first.Id, result.Keys["b"]);
        Assert.Equal(second.Id, result.Keys["a"]);
        Assert.Equal(new string[] { null, "second" }, host.Connection.Query<string>("SELECT Name FROM generated_keys ORDER BY Id"));
        Assert.Equal(2, host.GeneratedSqlCalls);
    }

    [Fact]
    public async Task InsertWithKeys_DefaultValues_ShouldReturnLongKey()
    {
        using var host = new Host();
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var row = new EmptyRow();
        var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<EmptyRow, long>(
            new[] { new InsertRow<EmptyRow>("empty", row) });
        Assert.Equal(WriteCompletionState.Committed, result.Completion);
        Assert.True(row.Id > 0);
        Assert.Equal(row.Id, result.Keys["empty"]);
        Assert.Equal(row.Id, host.Connection.QuerySingle<long>("SELECT Id FROM generated_empty_keys"));
        Assert.Equal(1, host.GeneratedSqlCalls);
    }

    [Fact]
    public async Task InsertWithKeys_SecondRowFailure_ShouldRollbackOwnedTransaction()
    {
        using var host = new Host();
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var first = new Row { Name = "duplicate" };
        var second = new Row { Name = "duplicate" };
        await Assert.ThrowsAsync<SqliteException>(() => ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<Row, int>(new[]
        {
            new InsertRow<Row>("one", first), new InsertRow<Row>("two", second)
        }));
        Assert.Equal(0, host.Connection.QuerySingle<int>("SELECT COUNT(*) FROM generated_keys"));
        // 事务回滚数据库；沿用现有合同，不回退已交付给实体的内存主键。
        Assert.True(first.Id > 0);
        Assert.Equal(0, second.Id);
        Assert.Equal(2, host.GeneratedSqlCalls);
        using var retry = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var result = await ((ISqlKeyInsertExecutor)retry).InsertWithKeysAsync<Row, int>(
            new[] { new InsertRow<Row>("retry", new Row { Name = "valid" }) });
        Assert.Equal(WriteCompletionState.Committed, result.Completion);
        Assert.Equal(1, host.Connection.QuerySingle<int>("SELECT COUNT(*) FROM generated_keys"));
    }

    [Fact]
    public async Task InsertWithKeys_SharedTransaction_ShouldLeaveCommitToOwner()
    {
        using var host = new Host();
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        var row = new Row { Name = "shared" };
        var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<Row, int>(
            new[] { new InsertRow<Row>("shared", row) });
        Assert.Equal(WriteCompletionState.Flushed, result.Completion);
        Assert.Equal(row.Id, result.Keys["shared"]);
        Assert.Equal(0, host.Connection.QuerySingle<int>("SELECT COUNT(*) FROM generated_keys"));
        await transaction.RollbackAsync();
        Assert.Equal(0, host.Connection.QuerySingle<int>("SELECT COUNT(*) FROM generated_keys"));
        Assert.Equal(1, host.GeneratedSqlCalls);
    }

    [Fact]
    public async Task InsertWithKeys_KeyOverflow_ShouldRollbackWithoutBackfill()
    {
        using var host = new Host();
        host.Connection.Execute("INSERT INTO sqlite_sequence(name,seq) VALUES('generated_keys',2147483647)");
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var row = new Row { Name = "overflow" };
        await Assert.ThrowsAsync<OverflowException>(() => ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<Row, int>(
            new[] { new InsertRow<Row>("overflow", row) }));
        Assert.Equal(0, row.Id);
        Assert.Equal(0, host.Connection.QuerySingle<int>("SELECT COUNT(*) FROM generated_keys"));
        Assert.Equal(1, host.GeneratedSqlCalls);
    }

    public sealed class Row { public int Id { get; set; } public string Name { get; set; } }
    public sealed class EmptyRow { public long Id { get; set; } }

    private sealed class Host : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "bing-generated-keys-" + Guid.NewGuid().ToString("N") + ".db");
        internal ServiceProvider Services { get; }
        internal SqliteConnection Connection { get; }
        internal int GeneratedSqlCalls;

        internal Host()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
            services.AddSqlGeneratedModels(new[]
            {
                new EntityModelMetadata(typeof(Row), "generated_keys", null, new[]
                {
                    EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true, isKey: true,
                        databaseGeneratedOption: DatabaseGeneratedOption.Identity, generatedGetter: static row => ((Row)row).Id,
                        generatedSetter: static (row, value) => ((Row)row).Id = (int)value),
                    EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                        generatedGetter: static row => ((Row)row).Name, generatedSetter: static (row, value) => ((Row)row).Name = (string)value)
                }, static () => new Row()) { GeneratedKeyInsertSql = provider => GetSql(provider, false) },
                new EntityModelMetadata(typeof(EmptyRow), "generated_empty_keys", null, new[]
                {
                    EntityPropertyMetadata.CreateGenerated(nameof(EmptyRow.Id), typeof(long), true, true, isKey: true,
                        databaseGeneratedOption: DatabaseGeneratedOption.Identity, generatedGetter: static row => ((EmptyRow)row).Id,
                        generatedSetter: static (row, value) => ((EmptyRow)row).Id = (long)value)
                }, static () => new EmptyRow()) { GeneratedKeyInsertSql = provider => GetSql(provider, true) }
            });
            var connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
            services.AddSqliteProvider();
            services.AddEntityKeyPolicies(keys => keys
                .ForGenerated<Row, int>(nameof(Row.Id), EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key)
                .ForGenerated<EmptyRow, long>(nameof(EmptyRow.Id), EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, key) => row.Id = key));
            Services = services.BuildServiceProvider();
            Connection = new SqliteConnection(connectionString);
            Connection.Execute("CREATE TABLE generated_keys(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT UNIQUE); CREATE TABLE generated_empty_keys(Id INTEGER PRIMARY KEY AUTOINCREMENT)");
        }

        private string GetSql(string provider, bool empty)
        {
            Interlocked.Increment(ref GeneratedSqlCalls);
            return provider != "bing.sqlite" ? null : empty
                ? "Insert Into `generated_empty_keys` DEFAULT VALUES RETURNING `Id`"
                : "Insert Into `generated_keys` (`Name`) Values (@_p_0) Returning `Id`";
        }

        public void Dispose() { Services.Dispose(); Connection.Dispose(); File.Delete(_path); }
    }
}
