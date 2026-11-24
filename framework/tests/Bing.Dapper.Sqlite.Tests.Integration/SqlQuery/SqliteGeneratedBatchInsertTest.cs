using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证多行生成 SQL 与静态参数绑定在 SQLite 中的真实执行。</summary>
public sealed class SqliteGeneratedBatchInsertTest
{
    private const string InsertSql = "Insert Into `generated_batch_rows` (`Id`, `display_name`, `Version`) " +
        "Values (@_p_0, @_p_1, @_p_2), (@_p_3, @_p_4, @_p_5), (@_p_6, @_p_7, @_p_8)";

    /// <summary>三行写入应使用完整生成 SQL、静态绑定并保存全部列值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertCombined_GeneratedOnly_ShouldExecuteThreeRows(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var rows = Batch(1, "first", 10, 2, "second", 20, 3, "third", 30);
        var command = host.Commands.InsertCombined(rows);

        AssertCommand(command, rows);
        Assert.Equal(3, await Execute(executor, command, asynchronous));
        AssertRows(host, rows);
    }

    /// <summary>共享事务中的多行写入由事务所有者回滚。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertCombined_InSharedTransaction_ShouldRollbackByOwner(bool asynchronous)
    {
        using var host = new TestHost();
        var rows = Batch(11, "transaction-one", 1, 12, "transaction-two", 2, 13, "transaction-three", 3);
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        var command = host.Commands.InsertCombined(rows);

        AssertCommand(command, rows);
        Assert.Equal(3, await Execute(executor, command, asynchronous));
        await transaction.RollbackAsync();

        Assert.Equal(0, host.CountRows());
    }

    /// <summary>中间行主键冲突应回滚整条语句，失败后同一执行器仍可继续写入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertCombined_WhenMiddleRowDuplicatesKey_ShouldRemainAtomicAndAllowNextCommand(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var duplicateRows = Batch(21, "before-duplicate", 1, 21, "duplicate-key", 2, 23, "after-duplicate", 3);
        var failedCommand = host.Commands.InsertCombined(duplicateRows);

        AssertCommand(failedCommand, duplicateRows);
        if (asynchronous)
            await Assert.ThrowsAsync<SqliteException>(() => executor.ExecuteWriteAsync(failedCommand));
        else
            Assert.Throws<SqliteException>(() => executor.ExecuteWrite(failedCommand));

        Assert.Equal(0, host.CountRows());

        var retryRows = Batch(31, "retry-one", 4, 32, "retry-two", 5, 33, "retry-three", 6);
        var retryCommand = host.Commands.InsertCombined(retryRows);
        AssertCommand(retryCommand, retryRows);
        Assert.Equal(3, await Execute(executor, retryCommand, asynchronous));
        AssertRows(host, retryRows);
    }

    private static Task<int> Execute(ISqlExecutor executor, SqlWriteCommand command, bool asynchronous) =>
        asynchronous ? executor.ExecuteWriteAsync(command) : Task.FromResult(executor.ExecuteWrite(command));

    private static void AssertCommand(SqlWriteCommand command, IReadOnlyCollection<Row> rows)
    {
        Assert.Equal(InsertSql, command.Sql);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(rows.SelectMany(row => new object[] { row.Id, row.Name, row.Version }),
            command.Parameters.Select(parameter => parameter.Value));
    }

    private static void AssertRows(TestHost host, IReadOnlyCollection<Row> expected)
    {
        var actual = host.Connection.Query<Row>(
            "SELECT Id, display_name AS Name, Version FROM generated_batch_rows ORDER BY Id").ToArray();
        var expectedRows = expected.OrderBy(row => row.Id).ToArray();
        Assert.Equal(expectedRows.Length, actual.Length);
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.Equal(expectedRows[i].Id, actual[i].Id);
            Assert.Equal(expectedRows[i].Name, actual[i].Name);
            Assert.Equal(expectedRows[i].Version, actual[i].Version);
        }
    }

    private static Row[] Batch(int id1, string name1, int version1, int id2, string name2, int version2,
        int id3, string name3, int version3) => new[]
    {
        new Row { Id = id1, Name = name1, Version = version1 },
        new Row { Id = id2, Name = name2, Version = version2 },
        new Row { Id = id3, Name = name3, Version = version3 }
    };

    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }

    private static EntityModelMetadata CreateModel() => new(typeof(Row), "generated_batch_rows", null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true, isKey: true,
                generatedGetter: static row => ((Row)row).Id, generatedSetter: static (row, value) => ((Row)row).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true, "display_name",
                generatedGetter: static row => ((Row)row).Name, generatedSetter: static (row, value) => ((Row)row).Name = (string)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int), true, true,
                generatedGetter: static row => ((Row)row).Version, generatedSetter: static (row, value) => ((Row)row).Version = (int)value)
        }, static () => new Row())
    {
        GeneratedMutationShapeSql = static (provider, operation, property, rows) =>
            provider == "bing.sqlite" && operation == SqlOperationKind.InsertValues && property == null && rows == 3
                ? InsertSql
                : null
    };

    private sealed class TestHost : IDisposable
    {
        private readonly string _connectionString;
        internal ServiceProvider Services { get; }
        internal SqliteConnection Connection { get; }
        internal ISqlCombinedInsertMutationBuilder Commands { get; }

        internal TestHost()
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = "generated_batch_" + Guid.NewGuid().ToString("N"),
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                Pooling = false
            }.ToString();

            var model = CreateModel();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlCore();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
            services.AddSqlGeneratedModels(new[] { model });
            services.AddSqlDataSource("default", DatabaseType.Sqlite, _connectionString);
            services.AddSqliteProvider();
            Services = services.BuildServiceProvider();
            Commands = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices(
                metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
                entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(new[] { model })));

            Connection = new SqliteConnection(_connectionString);
            Connection.Open();
            Connection.Execute("CREATE TABLE generated_batch_rows (Id INTEGER PRIMARY KEY, display_name TEXT NOT NULL, Version INTEGER NOT NULL)");
        }

        internal ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();

        internal int CountRows() => Connection.QuerySingle<int>("SELECT COUNT(*) FROM generated_batch_rows");

        public void Dispose()
        {
            Services.Dispose();
            Connection.Dispose();
        }
    }
}
