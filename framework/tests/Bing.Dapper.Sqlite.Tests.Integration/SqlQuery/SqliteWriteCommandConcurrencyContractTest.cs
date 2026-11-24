using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;
using Bing.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>独立写入命令的并发约束在四个公开执行入口中必须保持一致。</summary>
public sealed class SqliteWriteCommandConcurrencyContractTest
{
    public static IEnumerable<object[]> InvalidRowCounts()
    {
        foreach (var returning in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        foreach (var rowCount in new[] { 0, 2 })
            yield return new object[] { returning, asynchronous, rowCount };
    }

    /// <summary>校验在执行生命周期内失败，清理自有连接，后续命令仍可正常执行。</summary>
    [Theory]
    [MemberData(nameof(InvalidRowCounts))]
    public async Task Execute_WhenValidatedRowCountDiffersFromOne_ShouldThrowAndReleaseConnection(
        bool returning, bool asynchronous, int rowCount)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        using var connection = new SqliteConnection(host.ConnectionString);
        SqlExecutionRuntimeBinding.BindOwnedConnection(executor, connection, SqlConnectionSource.DataSource);
        var command = CreateCommand(executor, returning, rowCount).WithValidateAffectedRows(true);

        await Assert.ThrowsAsync<ConcurrencyException>(() => Execute(executor, command, asynchronous));
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(1, await Execute(executor, CreateCommand(executor, returning, 1)
            .WithValidateAffectedRows(true), asynchronous));
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    /// <summary>单行成功与显式关闭校验均保持原有返回行数语义。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Execute_WhenValidationIsSatisfiedOrDisabled_ShouldReturnActualCount(bool returning, bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        Assert.Equal(1, await Execute(executor, CreateCommand(executor, returning, 1)
            .WithValidateAffectedRows(true), asynchronous));
        Assert.Equal(0, await Execute(executor, CreateCommand(executor, returning, 0), asynchronous));
        Assert.Equal(2, await Execute(executor, CreateCommand(executor, returning, 2), asynchronous));
    }

    /// <summary>共享事务中的校验异常必须保留给调用方，并允许外层回滚已执行的多行写入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_WhenSharedTransactionFailsValidation_ShouldAllowOwnerRollback(bool returning)
    {
        using var host = new TestHost();
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        var command = CreateCommand(executor, returning, 2).WithValidateAffectedRows(true);

        await Assert.ThrowsAsync<ConcurrencyException>(() => Execute(executor, command, asynchronous: true));
        await transaction.RollbackAsync();

        using var connection = new SqliteConnection(host.ConnectionString);
        connection.Open();
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT SUM(Counter) FROM command_concurrency";
        Assert.Equal(30L, read.ExecuteScalar());
    }

    private static SqlWriteCommand CreateCommand(ISqlExecutor executor, bool returning, int rowCount)
    {
        var builder = executor.CreateUpdateBuilder().Update(new SqlTableReference { TableName = "command_concurrency" })
            .Set("Counter", 99);
        if (rowCount == 2) builder.AllowAllRows();
        else builder.Where("Id", rowCount == 0 ? -1 : 1);
        if (returning) builder.Returning("Id", "Counter");
        var command = builder.BuildCommand();
        var expectedSql = "Update `command_concurrency` Set `Counter` = @_p_0" +
            (rowCount == 2 ? "" : " Where `Id`=@_p_1") +
            (returning ? " Returning `Id`, `Counter`" : "");
        Assert.Equal(expectedSql, command.Sql);
        return command;
    }

    private static async Task<int> Execute(ISqlExecutor executor, SqlWriteCommand command, bool asynchronous)
    {
        if (command.HasReturning)
            return asynchronous
                ? (await executor.ExecuteReturningAsync<Report>(command)).Count
                : executor.ExecuteReturning<Report>(command).Count;
        return asynchronous ? await executor.ExecuteWriteAsync(command) : executor.ExecuteWrite(command);
    }

    public sealed class Report
    {
        public int Id { get; set; }
        public int Counter { get; set; }
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "bing-command-concurrency-" + Guid.NewGuid().ToString("N") + ".db");
        internal string ConnectionString { get; }
        internal ServiceProvider Services { get; }

        internal TestHost()
        {
            ConnectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlCore();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
            services.AddSqlGeneratedModels(new[]
            {
                new EntityModelMetadata(typeof(Report), "command_concurrency", null, new[]
                {
                    EntityPropertyMetadata.CreateGenerated(nameof(Report.Id), typeof(int), true, true, "Id",
                        generatedSetter: static (row, value) => ((Report)row).Id = (int)value),
                    EntityPropertyMetadata.CreateGenerated(nameof(Report.Counter), typeof(int), true, true, "Counter",
                        generatedSetter: static (row, value) => ((Report)row).Counter = (int)value)
                }, static () => new Report())
            });
            services.AddSqlDataSource("default", DatabaseType.Sqlite, ConnectionString);
            services.AddSqliteProvider();
            Services = services.BuildServiceProvider();
            using var executor = CreateExecutor();
            executor.ExecuteSql("CREATE TABLE command_concurrency (Id INTEGER PRIMARY KEY, Counter INTEGER NOT NULL)");
            executor.ExecuteSql("INSERT INTO command_concurrency VALUES (1, 10), (2, 20)");
        }

        internal ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();

        public void Dispose()
        {
            Services.Dispose();
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
