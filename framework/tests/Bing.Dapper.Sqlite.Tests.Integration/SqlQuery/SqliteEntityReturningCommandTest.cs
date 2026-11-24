using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;
using Bing.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>实体 Returning 命令复用严格模式静态绑定、读取与事务所有权。</summary>
public sealed class SqliteEntityReturningCommandTest
{
    private const string Projection = " Returning `Id` As `Id`, `display_name` As `Name`, `Version` As `Version`, `NameLength` As `NameLength`";
    private const string InsertSql = "Insert Into `entity_returning` (`display_name`, `Version`) Values (@_p_0, @_p_1)" + Projection;
    private const string UpdateSql = "Update `entity_returning` Set `display_name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3" + Projection;
    private const string DeleteSql = "Delete From `entity_returning` Where `Id` = @_p_0 And `Version` = @_p_1" + Projection;

    /// <summary>完整生成 SQL、参数和 reader 必须实际执行，数据库生成列从返回行读取。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntityReturning_ShouldExecuteGeneratedCrudAndReadComputedValues(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var source = new ReturningRow { Name = "inserted", Version = 1 };
        var insert = host.Commands.InsertReturning(source);
        AssertCommand(insert, InsertSql, SqlOperationKind.InsertValues, new object[] { "inserted", 1 });
        var saved = Assert.Single(await Execute(executor, insert, asynchronous));
        Assert.True(saved.Id > 0);
        Assert.Equal(8, saved.NameLength);
        Assert.Equal(0, source.Id); // Returning 返回独立实体，主键回填仍使用既有 InsertWithKeys。

        saved.Name = "next";
        saved.Version = 2;
        var update = host.Commands.UpdateReturning(saved,
            new SqlUpdateOptions<ReturningRow>().Original(row => row.Version, 1));
        AssertCommand(update, UpdateSql, SqlOperationKind.Update, new object[] { "next", 2, saved.Id, 1 });
        Assert.True(update.ValidateAffectedRows);
        var updated = Assert.Single(await Execute(executor, update, asynchronous));
        Assert.Equal("next", updated.Name);
        Assert.Equal(4, updated.NameLength);
        Assert.Equal(2, updated.Version);

        var delete = host.Commands.DeleteReturning(updated);
        AssertCommand(delete, DeleteSql, SqlOperationKind.Delete, new object[] { saved.Id, 2 });
        var deleted = Assert.Single(await Execute(executor, delete, asynchronous));
        Assert.Equal(updated.Id, deleted.Id);
        Assert.Equal("next", deleted.Name);
        Assert.Equal(0, host.CountRows());
    }

    /// <summary>筛选写列时回退渲染，但返回列仍是整行，不能遗漏未写入的字段。</summary>
    [Fact]
    public void EntityReturning_WhenWriteColumnsAreFiltered_ShouldReturnUnchangedColumns()
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var saved = Assert.Single(executor.ExecuteReturning<ReturningRow>(host.Commands.InsertReturning(
            new ReturningRow { Name = "before", Version = 3 })));
        saved.Name = "after";
        saved.Version = 999;
        var command = host.Commands.UpdateReturning(saved, new SqlUpdateOptions<ReturningRow>
        {
            IncludeProperties = new[] { nameof(ReturningRow.Name) }
        }.Original(row => row.Version, 3));

        Assert.Equal("Update `entity_returning` Set `display_name` = @_p_0 Where `Id` = @_p_1 And `Version` = @_p_2" + Projection,
            command.Sql);
        var result = Assert.Single(executor.ExecuteReturning<ReturningRow>(command));
        Assert.Equal("after", result.Name);
        Assert.Equal(3, result.Version);
        Assert.Equal(5, result.NameLength);
    }

    /// <summary>生成式命令也必须保留并发异常；失败后执行器可以继续使用。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntityReturning_WhenConcurrencyDoesNotMatch_ShouldThrowAndAllowNextCommand(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var saved = Assert.Single(await Execute(executor, host.Commands.InsertReturning(
            new ReturningRow { Name = "stable", Version = 1 }), asynchronous));
        saved.Version = 2;
        var stale = host.Commands.UpdateReturning(saved);
        if (asynchronous)
            await Assert.ThrowsAsync<ConcurrencyException>(() => executor.ExecuteReturningAsync<ReturningRow>(stale));
        else
            Assert.Throws<ConcurrencyException>(() => { executor.ExecuteReturning<ReturningRow>(stale); });

        saved.Version = 1;
        Assert.Single(await Execute(executor, host.Commands.DeleteReturning(saved), asynchronous));
        Assert.Equal(0, host.CountRows());
    }

    /// <summary>三种命令均只借用共享事务，由外层回滚数据库状态。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntityReturning_InSharedTransaction_ShouldLeaveRollbackToOwner(bool asynchronous)
    {
        using var host = new TestHost();
        using (var executor = host.CreateExecutor())
            executor.ExecuteSql("INSERT INTO entity_returning (display_name, Version) VALUES ('original', 1)");
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var shared = transaction.CreateExecutor();
        var updated = Assert.Single(await Execute(shared, host.Commands.UpdateReturning(
            new ReturningRow { Id = 1, Name = "changed", Version = 1 }), asynchronous));
        Assert.Equal("changed", updated.Name);
        Assert.Single(await Execute(shared, host.Commands.DeleteReturning(updated), asynchronous));
        Assert.Single(await Execute(shared, host.Commands.InsertReturning(
            new ReturningRow { Name = "temporary", Version = 1 }), asynchronous));
        await transaction.RollbackAsync();

        Assert.Equal(1, host.CountRows());
        Assert.Equal("original", host.ReadName());
    }

    private static Task<List<ReturningRow>> Execute(ISqlExecutor executor, SqlWriteCommand command, bool asynchronous) =>
        asynchronous ? executor.ExecuteReturningAsync<ReturningRow>(command) :
            Task.FromResult(executor.ExecuteReturning<ReturningRow>(command));

    private static void AssertCommand(SqlWriteCommand command, string sql, SqlOperationKind operation, object[] values)
    {
        Assert.Equal(sql, command.Sql);
        Assert.True(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(operation, command.OperationKind);
        Assert.Equal(values, command.Parameters.Select(parameter => parameter.Value));
    }

    public sealed class ReturningRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
        public int NameLength { get; set; }
    }

    private static EntityModelMetadata CreateModel() => new(typeof(ReturningRow), "entity_returning", null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningRow.Id), typeof(int), true, true,
                isKey: true, databaseGeneratedOption: DatabaseGeneratedOption.Identity,
                generatedGetter: static row => ((ReturningRow)row).Id,
                generatedSetter: static (row, value) => ((ReturningRow)row).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningRow.Name), typeof(string), true, true, "display_name",
                generatedGetter: static row => ((ReturningRow)row).Name,
                generatedSetter: static (row, value) => ((ReturningRow)row).Name = (string)value),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningRow.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: static row => ((ReturningRow)row).Version,
                generatedSetter: static (row, value) => ((ReturningRow)row).Version = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningRow.NameLength), typeof(int), true, true,
                databaseGeneratedOption: DatabaseGeneratedOption.Computed,
                generatedGetter: static row => ((ReturningRow)row).NameLength,
                generatedSetter: static (row, value) => ((ReturningRow)row).NameLength = (int)value)
        }, static () => new ReturningRow())
    {
        GeneratedMutationReturningSql = static (provider, operation) => provider != "bing.sqlite" ? null : operation switch
        {
            SqlOperationKind.InsertValues => InsertSql,
            SqlOperationKind.Update => UpdateSql,
            SqlOperationKind.Delete => DeleteSql,
            _ => null
        }
    };

    private sealed class TestHost : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "bing-entity-returning-" + Guid.NewGuid().ToString("N") + ".db");
        private readonly string _connectionString;
        internal ServiceProvider Services { get; }
        internal ISqlEntityMutationCommandBuilder Commands { get; }

        internal TestHost()
        {
            _connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlCore();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
            var model = CreateModel();
            services.AddSqlGeneratedModels(new[] { model });
            services.AddSqlDataSource("default", DatabaseType.Sqlite, _connectionString);
            services.AddSqliteProvider();
            Services = services.BuildServiceProvider();
            Commands = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices(
                metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
                entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(new[] { model })));
            using var executor = CreateExecutor();
            executor.ExecuteSql("CREATE TABLE entity_returning (Id INTEGER PRIMARY KEY AUTOINCREMENT, display_name TEXT, Version INTEGER NOT NULL, NameLength INTEGER GENERATED ALWAYS AS (length(display_name)) STORED)");
        }

        internal ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();
        internal int CountRows() => Convert.ToInt32(ReadScalar("SELECT COUNT(*) FROM entity_returning"));
        internal string ReadName() => (string)ReadScalar("SELECT display_name FROM entity_returning WHERE Id = 1");
        private object ReadScalar(string sql)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }
        public void Dispose()
        {
            Services.Dispose();
            File.Delete(_path);
        }
    }
}
