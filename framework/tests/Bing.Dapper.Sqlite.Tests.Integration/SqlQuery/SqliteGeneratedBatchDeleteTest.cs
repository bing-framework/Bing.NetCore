using System;
using System.Linq;
using System.Threading.Tasks;
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

/// <summary>验证生成式单主键批量 Delete SQL 在 SQLite 中的执行、事务与语句原子性。</summary>
public sealed class SqliteGeneratedBatchDeleteTest
{
    private const string TableName = "generated_batch_delete_rows";
    private const string DeleteThreeSql = "Delete From `generated_batch_delete_rows` Where `Id` In (@_p_0,@_p_1,@_p_2)";
    private const string DeleteFourSql = "Delete From `generated_batch_delete_rows` Where `Id` In (@_p_0,@_p_1,@_p_2,@_p_3)";

    /// <summary>重复键和不存在的键不改变 IN 查询结果，受影响行数反映实际删除数量。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteCombined_GeneratedOnly_ShouldDeleteSelectedRows(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        host.Seed(1, 2, 3);
        var rows = Rows(1, 2, 2, 99);
        var command = host.Commands.DeleteCombined(rows);

        AssertCommand(command, DeleteFourSql, new[] { 1, 2, 2, 99 });
        Assert.Equal(2, await Execute(executor, command, asynchronous));
        Assert.Equal(new[] { 3 }, host.ReadIds());
    }

    /// <summary>共享事务中的批量删除由事务所有者回滚，不由查询执行器提交。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteCombined_InSharedTransaction_ShouldRollbackByOwner(bool asynchronous)
    {
        using var host = new TestHost();
        host.Seed(11, 12, 13);
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        var rows = Rows(11, 12, 13);
        var command = host.Commands.DeleteCombined(rows);

        AssertCommand(command, DeleteThreeSql, new[] { 11, 12, 13 });
        Assert.Equal(3, await Execute(executor, command, asynchronous));
        await transaction.RollbackAsync();

        Assert.Equal(new[] { 11, 12, 13 }, host.ReadIds());
    }

    /// <summary>触发器中途拒绝删除时 SQLite 回滚整条语句，移除触发器后执行器仍可复用。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteCombined_WhenTriggerAbortsMiddleRow_ShouldRemainAtomicAndAllowRetry(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        host.Seed(21, 22, 23);
        host.Connection.Execute("CREATE TRIGGER fail_batch_delete BEFORE DELETE ON " + TableName +
            " WHEN OLD.Id = 22 BEGIN SELECT RAISE(ABORT, 'batch delete failure'); END;");
        var rows = Rows(21, 22, 23);
        var command = host.Commands.DeleteCombined(rows);

        AssertCommand(command, DeleteThreeSql, new[] { 21, 22, 23 });
        if (asynchronous)
            await Assert.ThrowsAsync<SqliteException>(() => executor.ExecuteWriteAsync(command));
        else
            Assert.Throws<SqliteException>(() => executor.ExecuteWrite(command));

        Assert.Equal(new[] { 21, 22, 23 }, host.ReadIds());
        host.Connection.Execute("DROP TRIGGER fail_batch_delete;");

        Assert.Equal(3, await Execute(executor, command, asynchronous));
        Assert.Empty(host.ReadIds());
    }

    private static Row[] Rows(params int[] ids) => ids.Select(id => new Row { Id = id }).ToArray();

    private static Task<int> Execute(ISqlExecutor executor, SqlWriteCommand command, bool asynchronous) =>
        asynchronous ? executor.ExecuteWriteAsync(command) : Task.FromResult(executor.ExecuteWrite(command));

    private static void AssertCommand(SqlWriteCommand command, string expectedSql, int[] expectedIds)
    {
        Assert.Equal(expectedSql, command.Sql);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(expectedIds.Select(id => (object)id), command.Parameters.Select(parameter => parameter.Value));
    }

    public sealed class Row
    {
        public int Id { get; set; }
    }

    private static EntityModelMetadata CreateModel() => new(typeof(Row), TableName, null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true, isKey: true,
                generatedGetter: static row => ((Row)row).Id,
                generatedSetter: static (row, value) => ((Row)row).Id = (int)value)
        }, static () => new Row())
    {
        GeneratedMutationShapeSql = static (provider, operation, property, rows) =>
            provider == "bing.sqlite" && operation == SqlOperationKind.Delete && property == null && rows > 1
                ? GetDeleteSql(rows)
                : null
    };

    private static string GetDeleteSql(int rows) => "Delete From `" + TableName + "` Where `Id` In (" +
        string.Join(",", Enumerable.Range(0, rows).Select(index => "@_p_" + index)) + ")";

    private sealed class TestHost : IDisposable
    {
        private readonly string _connectionString;
        internal ServiceProvider Services { get; }
        internal SqliteConnection Connection { get; }
        internal ISqlCombinedDeleteMutationBuilder Commands { get; }

        internal TestHost()
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = "generated_batch_delete_" + Guid.NewGuid().ToString("N"),
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
            Connection.Execute("CREATE TABLE " + TableName + " (Id INTEGER PRIMARY KEY)");
        }

        internal ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();

        internal void Seed(params int[] ids)
        {
            foreach (var id in ids)
                Connection.Execute("INSERT INTO " + TableName + " (Id) VALUES (@Id)", new { Id = id });
        }

        internal int[] ReadIds() => Connection.Query<int>("SELECT Id FROM " + TableName + " ORDER BY Id").ToArray();

        public void Dispose()
        {
            Services.Dispose();
            Connection.Dispose();
        }
    }
}
