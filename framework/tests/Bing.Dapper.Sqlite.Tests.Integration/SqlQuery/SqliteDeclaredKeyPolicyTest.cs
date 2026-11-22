using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>基类写入合同必须从主键准备到回填始终使用同一策略。</summary>
public sealed class SqliteDeclaredKeyPolicyTest
{
    [Theory]
    [InlineData(SqlExecutionMode.Compatible, false)]
    [InlineData(SqlExecutionMode.Compatible, true)]
    [InlineData(SqlExecutionMode.GeneratedOnly, false)]
    [InlineData(SqlExecutionMode.GeneratedOnly, true)]
    public async Task Insert_WithBaseContract_ShouldUseBasePolicy(SqlExecutionMode mode, bool asynchronous)
    {
        using var host = new Host(mode);
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var row = new DerivedRow { Id = 900, Name = "single" };
        var affected = asynchronous ? await executor.InsertAsync<BaseRow>(row) : executor.Insert<BaseRow>(row);
        Assert.Equal(1, affected);
        Assert.Equal(101, ((BaseRow)row).Id);
        Assert.Equal(900, row.Id);
        Assert.Equal(101, host.Connection.QuerySingle<long>("SELECT Id FROM declared_keys"));
    }

    [Theory]
    [InlineData(SqlExecutionMode.Compatible, false)]
    [InlineData(SqlExecutionMode.Compatible, true)]
    [InlineData(SqlExecutionMode.GeneratedOnly, false)]
    [InlineData(SqlExecutionMode.GeneratedOnly, true)]
    public async Task InsertBatch_WithBaseContract_ShouldUseBasePolicy(SqlExecutionMode mode, bool asynchronous)
    {
        using var host = new Host(mode);
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        BaseRow[] rows = { new DerivedRow { Id = 900, Name = "a" }, new DerivedRow { Id = 901, Name = "b" } };
        var affected = asynchronous ? await executor.InsertBatchAsync(rows) : executor.InsertBatch(rows);
        Assert.Equal(2, affected);
        Assert.Equal(new long[] { 101, 102 }, rows.Select(row => row.Id));
        Assert.Equal(new long[] { 900, 901 }, rows.Cast<DerivedRow>().Select(row => row.Id));
        Assert.Equal(new long[] { 101, 102 }, host.Connection.Query<long>("SELECT Id FROM declared_keys ORDER BY Id"));
    }

    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    public async Task InsertWithKeys_WithBaseContract_ShouldUseBasePolicy(SqlExecutionMode mode)
    {
        using var host = new Host(mode);
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var row = new DerivedRow { Id = 900, Name = "keys" };
        var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<BaseRow, long>(
            new[] { new InsertRow<BaseRow>("row", row) });
        Assert.Equal(WriteCompletionState.Committed, result.Completion);
        Assert.Equal(101, result.Keys["row"]);
        Assert.Equal(101, ((BaseRow)row).Id);
        Assert.Equal(900, row.Id);
        Assert.Equal(101, host.Connection.QuerySingle<long>("SELECT Id FROM declared_keys"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedOnly_WithReflectiveDerivedPolicy_ShouldUseSelectedGeneratedPolicy(bool returnKeys)
    {
        using var host = new Host(SqlExecutionMode.GeneratedOnly, reflectiveDerivedPolicy: true);
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var row = new DerivedRow { Id = 900, Name = "static-policy" };
        if (returnKeys)
        {
            var result = await ((ISqlKeyInsertExecutor)executor).InsertWithKeysAsync<BaseRow, long>(
                new[] { new InsertRow<BaseRow>("row", row) });
            Assert.Equal(101, result.Keys["row"]);
        }
        else Assert.Equal(1, await executor.InsertAsync<BaseRow>(row));
        Assert.Equal(101, ((BaseRow)row).Id);
        Assert.Equal(900, row.Id);
        Assert.Equal(101, host.Connection.QuerySingle<long>("SELECT Id FROM declared_keys"));
    }

    [Theory]
    [InlineData(SqlExecutionMode.Compatible, 0)]
    [InlineData(SqlExecutionMode.Compatible, 1)]
    [InlineData(SqlExecutionMode.Compatible, 2)]
    [InlineData(SqlExecutionMode.Compatible, 3)]
    [InlineData(SqlExecutionMode.Compatible, 4)]
    [InlineData(SqlExecutionMode.GeneratedOnly, 0)]
    [InlineData(SqlExecutionMode.GeneratedOnly, 1)]
    [InlineData(SqlExecutionMode.GeneratedOnly, 2)]
    [InlineData(SqlExecutionMode.GeneratedOnly, 3)]
    [InlineData(SqlExecutionMode.GeneratedOnly, 4)]
    public async Task Identity_WithDerivedPolicy_ShouldRejectAssignedBaseKeyBeforeWrite(SqlExecutionMode mode, int entrypoint)
    {
        using var host = new Host(mode, identity: true);
        using var executor = host.Services.GetRequiredService<ISqlExecutorFactory>().Create();
        var row = new DerivedRow { Id = 900, Name = "invalid" };
        ((BaseRow)row).Id = 77;
        switch (entrypoint)
        {
            case 0: Assert.Throws<InvalidOperationException>(() => executor.Insert<BaseRow>(row)); break;
            case 1: await Assert.ThrowsAsync<InvalidOperationException>(() => executor.InsertAsync<BaseRow>(row)); break;
            case 2: Assert.Throws<InvalidOperationException>(() => executor.InsertBatch(new BaseRow[] { row })); break;
            case 3: await Assert.ThrowsAsync<InvalidOperationException>(() => executor.InsertBatchAsync(new BaseRow[] { row })); break;
            default:
                await Assert.ThrowsAsync<InvalidOperationException>(() => ((ISqlKeyInsertExecutor)executor)
                    .InsertWithKeysAsync<BaseRow, long>(new[] { new InsertRow<BaseRow>("invalid", row) }));
                break;
        }
        Assert.Equal(0, host.Connection.QuerySingle<int>("SELECT COUNT(*) FROM declared_keys"));
    }

    public class BaseRow
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }

    public sealed class DerivedRow : BaseRow
    {
        public new long Id { get; set; }
    }

    private sealed class Counter : IClientKeyGenerator<long>
    {
        private long _value = 100;
        public string Name => "default";
        public long Next(CancellationToken cancellationToken = default) => Interlocked.Increment(ref _value);
    }

    private sealed class Host : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "bing-declared-keys-" + Guid.NewGuid().ToString("N") + ".db");
        internal ServiceProvider Services { get; }
        internal SqliteConnection Connection { get; }

        internal Host(SqlExecutionMode mode, bool identity = false, bool reflectiveDerivedPolicy = false)
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            var services = new ServiceCollection();
            services.AddLogging();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
            services.AddSqlGeneratedModels(new[] { new EntityModelMetadata(typeof(BaseRow), "declared_keys", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(BaseRow.Id), typeof(long), true, true, isKey: true,
                    databaseGeneratedOption: identity ? DatabaseGeneratedOption.Identity : DatabaseGeneratedOption.None,
                    generatedGetter: static row => ((BaseRow)row).Id, generatedSetter: static (row, value) => ((BaseRow)row).Id = (long)value),
                EntityPropertyMetadata.CreateGenerated(nameof(BaseRow.Name), typeof(string), true, true,
                    generatedGetter: static row => ((BaseRow)row).Name, generatedSetter: static (row, value) => ((BaseRow)row).Name = (string)value)
            }, static () => new BaseRow()) });
            services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
            services.AddSqliteProvider();
            services.AddEntityKeyPolicies(keys =>
            {
                keys.ForGenerated<BaseRow, long>(nameof(BaseRow.Id), identity ? EntityKeyGenerationKind.DatabaseIdentity : EntityKeyGenerationKind.ClientSnowflake,
                    static row => row.Id, static (row, value) => row.Id = value);
                if (reflectiveDerivedPolicy) keys.For<DerivedRow, long>(row => row.Id, EntityKeyGenerationKind.Assigned);
                else keys.ForGenerated<DerivedRow, long>(nameof(DerivedRow.Id), EntityKeyGenerationKind.Assigned,
                    static row => row.Id, static (row, value) => row.Id = value);
            });
            services.AddSingleton<IClientKeyGenerator<long>, Counter>();
            Services = services.BuildServiceProvider();
            Connection = new SqliteConnection(connectionString);
            Connection.Execute("CREATE TABLE declared_keys(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT)");
        }

        public void Dispose()
        {
            Services.Dispose();
            Connection.Dispose();
            File.Delete(_path);
        }
    }
}
