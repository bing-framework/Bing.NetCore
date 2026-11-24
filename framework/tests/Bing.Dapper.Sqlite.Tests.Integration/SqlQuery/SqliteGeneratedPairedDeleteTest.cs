using System.Linq;
using System.Threading.Tasks;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;
using Bing.Exceptions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证静态 Getter 元数据下复合键与并发令牌的配对删除执行。</summary>
public sealed class SqliteGeneratedPairedDeleteTest
{
    private const string TableName = "generated_paired_delete_rows";
    private const string DeleteTwoSql = "Delete From `generated_paired_delete_rows` Where " +
        "((`tenant_id` = @_p_0 And `record_id` = @_p_1 And `version_no` = @_p_2) Or " +
        "(`tenant_id` = @_p_3 And `record_id` = @_p_4 And `version_no` = @_p_5))";

    /// <summary>每个实体的复合键和版本条件必须绑定在同一 OR 分支，不能误删交叉组合。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteBatch_GeneratedOnly_ShouldKeepEachKeyAndVersionPaired(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        host.Seed(
            new Row { TenantId = 1, Id = 10, Version = 101 },
            new Row { TenantId = 2, Id = 20, Version = 202 },
            new Row { TenantId = 1, Id = 20, Version = 202 },
            new Row { TenantId = 2, Id = 10, Version = 101 });
        var rows = new[]
        {
            new Row { TenantId = 1, Id = 10, Version = 101 },
            new Row { TenantId = 2, Id = 20, Version = 202 }
        };

        var command = host.Commands.DeleteCombined(rows);
        AssertCommand(command, new object[] { 1, 10, 101, 2, 20, 202 }, validateAffectedRows: true);
        Assert.Equal(2, await ExecuteBatch(executor, rows, new SqlBatchDeleteOptions { BatchSize = 2 }, asynchronous));
        Assert.Equal(new[] { "1:20:202", "2:10:101" }, host.ReadRows());
    }

    /// <summary>DeleteOptions.Original 覆盖实体当前令牌，并对批次中每个实体分别绑定原值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteBatch_WhenOriginalVersionIsConfigured_ShouldBindItForEachEntity(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        host.Seed(
            new Row { TenantId = 3, Id = 30, Version = 7 },
            new Row { TenantId = 3, Id = 31, Version = 7 });
        var rows = new[]
        {
            new Row { TenantId = 3, Id = 30, Version = 70 },
            new Row { TenantId = 3, Id = 31, Version = 80 }
        };
        var deleteOptions = new SqlDeleteOptions<Row>().Original(row => row.Version, 7);
        var command = host.Commands.DeleteCombined(rows, deleteOptions);

        AssertCommand(command, new object[] { 3, 30, 7, 3, 31, 7 }, validateAffectedRows: true);
        Assert.Equal(2, await ExecuteBatch(executor, rows,
            new SqlBatchDeleteOptions { BatchSize = 2, DeleteOptions = deleteOptions }, asynchronous));
        Assert.Empty(host.ReadRows());
    }

    /// <summary>共享事务中的生成式批量删除由外层事务所有者回滚。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteBatch_InSharedTransaction_ShouldRollbackByOwner(bool asynchronous)
    {
        using var host = new TestHost();
        host.Seed(
            new Row { TenantId = 4, Id = 40, Version = 4 },
            new Row { TenantId = 4, Id = 41, Version = 4 });
        var rows = new[]
        {
            new Row { TenantId = 4, Id = 40, Version = 4 },
            new Row { TenantId = 4, Id = 41, Version = 4 }
        };

        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        var command = host.Commands.DeleteCombined(rows);
        AssertCommand(command, new object[] { 4, 40, 4, 4, 41, 4 }, validateAffectedRows: true);
        Assert.Equal(2, await ExecuteBatch(executor, rows, new SqlBatchDeleteOptions { BatchSize = 2 }, asynchronous));
        await transaction.RollbackAsync();

        Assert.Equal(new[] { "4:40:4", "4:41:4" }, host.ReadRows());
    }

    /// <summary>并发冲突默认抛异常并回滚整个批次，ReturnAffectedRows 则返回真实删除数。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DeleteBatch_WhenOneVersionConflicts_ShouldApplyConfiguredBehavior(bool asynchronous,
        bool returnAffectedRows)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        host.Seed(
            new Row { TenantId = 5, Id = 50, Version = 5 },
            new Row { TenantId = 5, Id = 51, Version = 5 });
        var rows = new[]
        {
            new Row { TenantId = 5, Id = 50, Version = 55 },
            new Row { TenantId = 5, Id = 51, Version = 5 }
        };
        var deleteOptions = new SqlDeleteOptions<Row>
        {
            ConcurrencyConflictBehavior = returnAffectedRows
                ? SqlConcurrencyConflictBehavior.ReturnAffectedRows
                : SqlConcurrencyConflictBehavior.Throw
        };
        var command = host.Commands.DeleteCombined(rows, deleteOptions);
        AssertCommand(command, new object[] { 5, 50, 55, 5, 51, 5 }, validateAffectedRows: !returnAffectedRows);
        var batchOptions = new SqlBatchDeleteOptions { BatchSize = 2, DeleteOptions = deleteOptions };

        if (returnAffectedRows)
        {
            Assert.Equal(1, await ExecuteBatch(executor, rows, batchOptions, asynchronous));
            Assert.Equal(new[] { "5:50:5" }, host.ReadRows());
        }
        else
        {
            await Assert.ThrowsAsync<ConcurrencyException>(() => ExecuteBatch(executor, rows, batchOptions, asynchronous));
            Assert.Equal(new[] { "5:50:5", "5:51:5" }, host.ReadRows());
        }
    }

    private static Task<int> ExecuteBatch(ISqlExecutor executor, Row[] rows, SqlBatchDeleteOptions options,
        bool asynchronous) => ExecuteBatchAsync(executor, rows, options, asynchronous);

    private static async Task<int> ExecuteBatchAsync(ISqlExecutor executor, Row[] rows,
        SqlBatchDeleteOptions options, bool asynchronous)
    {
        return asynchronous
            ? await executor.DeleteBatchAsync(rows, options)
            : executor.DeleteBatch(rows, options);
    }

    private static void AssertCommand(SqlWriteCommand command, object[] expectedParameters, bool validateAffectedRows)
    {
        Assert.Equal(DeleteTwoSql, command.Sql);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(validateAffectedRows, command.ValidateAffectedRows);
        Assert.Equal(expectedParameters, command.Parameters.Select(parameter => parameter.Value));
    }

    public sealed class Row
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public int Version { get; set; }
    }

    private static EntityModelMetadata CreateModel() => new(typeof(Row), TableName, null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.TenantId), typeof(int), true, true, "tenant_id",
                isKey: true, generatedGetter: static row => ((Row)row).TenantId,
                generatedSetter: static (row, value) => ((Row)row).TenantId = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true, "record_id",
                isKey: true, generatedGetter: static row => ((Row)row).Id,
                generatedSetter: static (row, value) => ((Row)row).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int), true, true, "version_no",
                isConcurrencyToken: true, generatedGetter: static row => ((Row)row).Version,
                generatedSetter: static (row, value) => ((Row)row).Version = (int)value)
        }, static () => new Row())
    {
        GeneratedMutationShapeSql = static (provider, operation, property, rows) =>
            provider == "bing.sqlite" && operation == SqlOperationKind.Delete && property == null && rows == 2
                ? DeleteTwoSql
                : null
    };

    private sealed class TestHost : System.IDisposable
    {
        private readonly string _connectionString;
        internal ServiceProvider Services { get; }
        internal SqliteConnection Connection { get; }
        internal ISqlCombinedDeleteMutationBuilder Commands { get; }

        internal TestHost()
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = "generated_paired_delete_" + System.Guid.NewGuid().ToString("N"),
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
            Connection.Execute("CREATE TABLE " + TableName + " (tenant_id INTEGER NOT NULL, " +
                "record_id INTEGER NOT NULL, version_no INTEGER NOT NULL, PRIMARY KEY (tenant_id, record_id))");
        }

        internal ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();

        internal void Seed(params Row[] rows)
        {
            foreach (var row in rows)
                Connection.Execute("INSERT INTO " + TableName + " (tenant_id, record_id, version_no) " +
                    "VALUES (@TenantId, @Id, @Version)", row);
        }

        internal string[] ReadRows() => Connection.Query<Row>(
                "SELECT tenant_id AS TenantId, record_id AS Id, version_no AS Version FROM " + TableName +
                " ORDER BY tenant_id, record_id")
            .Select(row => row.TenantId + ":" + row.Id + ":" + row.Version)
            .ToArray();

        public void Dispose()
        {
            Services.Dispose();
            Connection.Dispose();
        }
    }
}
