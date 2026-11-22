using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证 GeneratedOnly 执行器对独立 Returning DTO 的静态物化和事务边界。</summary>
public sealed class SqliteReturningProjectionContractTest
{
    private const string ProjectionSql = "UPDATE returning_projection SET Counter = Counter + 1 WHERE Id = 1 RETURNING 42 AS \"count_alias\", NULL AS \"nullable_alias\", Id AS \"id_alias\", 'projected-name' AS \"display_alias\"";

    /// <summary>返回列按任意顺序和别名绑定，NULL 保留 DTO 的属性初值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returning_ShouldMapReorderedAliasesAndPreserveNull(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var command = WriteCommand(ProjectionSql);

        Assert.Equal(ProjectionSql, command.Sql);
        var row = asynchronous
            ? Assert.Single(await executor.ExecuteReturningAsync<ReturningDto>(command))
            : Assert.Single(executor.ExecuteReturning<ReturningDto>(command));

        Assert.Equal(1, row.Id);
        Assert.Equal("projected-name", row.DisplayName);
        Assert.Equal("initial-nullable", row.NullableValue);
        Assert.Equal(42, row.Count);
    }

    /// <summary>结果列名使用最终映射覆盖后的列别名，而不是模型中的默认列名。</summary>
    [Fact]
    public void Returning_ShouldUseFinalMappingOverride()
    {
        using var host = new TestHost(overrideDisplayAlias: true);
        using var executor = host.CreateExecutor();
        const string sql = "UPDATE returning_projection SET Counter = Counter + 1 WHERE Id = 1 RETURNING 'mapped-name' AS \"final_display_alias\"";
        var command = WriteCommand(sql);

        Assert.Equal(sql, command.Sql);
        var row = Assert.Single(executor.ExecuteReturning<ReturningDto>(command));

        Assert.Equal("mapped-name", row.DisplayName);
        Assert.Equal("initial-nullable", row.NullableValue);
    }

    /// <summary>超出 Int32 的 SQLite 整数触发 checked 溢出，失败后自有连接关闭且执行器可继续读取。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returning_WhenIntegerOverflows_ShouldCloseOwnedConnectionAndAllowNextCommand(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        using var ownedConnection = new SqliteConnection(host.ConnectionString);
        SqlExecutionRuntimeBinding.BindOwnedConnection(executor, ownedConnection, SqlConnectionSource.DataSource);
        const string overflowSql = "UPDATE returning_projection SET Counter = Counter + 1 WHERE Id = 1 RETURNING 2147483648 AS \"count_alias\"";
        var overflowCommand = WriteCommand(overflowSql);
        Assert.Equal(overflowSql, overflowCommand.Sql);

        if (asynchronous)
            await Assert.ThrowsAsync<OverflowException>(() => executor.ExecuteReturningAsync<ReturningDto>(overflowCommand));
        else
            Assert.Throws<OverflowException>(() => executor.ExecuteReturning<ReturningDto>(overflowCommand));

        Assert.Equal(ConnectionState.Closed, ownedConnection.State);
        const string nextSql = "UPDATE returning_projection SET Counter = Counter WHERE Id = 1 RETURNING Id AS \"id_alias\"";
        var nextCommand = WriteCommand(nextSql);
        Assert.Equal(nextSql, nextCommand.Sql);
        Assert.Equal(1, Assert.Single(executor.ExecuteReturning<ReturningDto>(nextCommand)).Id);
        Assert.Equal(ConnectionState.Closed, ownedConnection.State);
    }

    /// <summary>转换失败后绑定连接关闭，同一执行器仍可继续执行 Returning。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returning_WhenValueCannotConvert_ShouldCloseOwnedConnectionAndAllowNextCommand(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        using var ownedConnection = new SqliteConnection(host.ConnectionString);
        SqlExecutionRuntimeBinding.BindOwnedConnection(executor, ownedConnection, SqlConnectionSource.DataSource);
        const string invalidSql = "UPDATE returning_projection SET Counter = Counter WHERE Id = 1 RETURNING 'not-an-integer' AS \"count_alias\"";
        var invalidCommand = WriteCommand(invalidSql);
        Assert.Equal(invalidSql, invalidCommand.Sql);

        if (asynchronous)
            await Assert.ThrowsAsync<FormatException>(() => executor.ExecuteReturningAsync<ReturningDto>(invalidCommand));
        else
            Assert.Throws<FormatException>(() => executor.ExecuteReturning<ReturningDto>(invalidCommand));

        Assert.Equal(ConnectionState.Closed, ownedConnection.State);
        const string nextSql = "UPDATE returning_projection SET Counter = Counter WHERE Id = 1 RETURNING Id AS \"id_alias\"";
        var nextCommand = WriteCommand(nextSql);
        Assert.Equal(nextSql, nextCommand.Sql);
        Assert.Equal(1, Assert.Single(executor.ExecuteReturning<ReturningDto>(nextCommand)).Id);
        Assert.Equal(ConnectionState.Closed, ownedConnection.State);
    }

    /// <summary>共享事务内返回的写入在外部回滚后不得留在数据库中。</summary>
    [Fact]
    public async Task Returning_InSharedTransaction_ShouldBeRolledBackByOwner()
    {
        using var host = new TestHost();
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        const string sql = "UPDATE returning_projection SET Counter = 99 WHERE Id = 1 RETURNING Counter AS \"count_alias\"";
        var command = WriteCommand(sql);
        Assert.Equal(sql, command.Sql);

        var row = Assert.Single(await executor.ExecuteReturningAsync<ReturningDto>(command));
        Assert.Equal(99, row.Count);
        const string readSql = "UPDATE returning_projection SET Counter = Counter WHERE Id = 1 RETURNING Counter AS \"count_alias\"";
        Assert.Equal(99, Assert.Single(executor.ExecuteReturning<ReturningDto>(WriteCommand(readSql))).Count);
        await transaction.RollbackAsync();

        Assert.Equal(10, host.ReadCounter());
    }

    private static SqlWriteCommand WriteCommand(string sql) => new(sql, Array.Empty<SqlParam>(),
        SqliteSqlProvider.Instance, SqlOperationKind.Update, hasReturning: true);

    private static EntityModelMetadata CreateResultModel() => new(typeof(ReturningDto), "returning_projection", null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningDto.Id), typeof(int), true, true, "id_alias",
                generatedSetter: static (row, value) => ((ReturningDto)row).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningDto.DisplayName), typeof(string), true, true, "display_alias",
                generatedSetter: static (row, value) => ((ReturningDto)row).DisplayName = (string)value),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningDto.NullableValue), typeof(string), true, true, "nullable_alias",
                generatedSetter: static (row, value) => ((ReturningDto)row).NullableValue = (string)value),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningDto.Count), typeof(int), true, true, "count_alias",
                generatedSetter: static (row, value) => ((ReturningDto)row).Count = (int)value)
        }, static () => new ReturningDto());

    /// <summary>独立结果 DTO，不依赖查询调用点或实体 CRUD 模型。</summary>
    public sealed class ReturningDto
    {
        public int Id { get; set; } = -1;
        public string DisplayName { get; set; } = "initial-display";
        public string NullableValue { get; set; } = "initial-nullable";
        public int Count { get; set; } = -1;
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "bing-returning-projection-" + Guid.NewGuid().ToString("N") + ".db");
        private readonly string _connectionString;

        internal ServiceProvider Services { get; }
        internal string ConnectionString => _connectionString;

        internal TestHost(bool overrideDisplayAlias = false)
        {
            _connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlCore();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
            services.AddSqlGeneratedModels(new[] { CreateResultModel() });
            services.AddSqlDataSource("default", DatabaseType.Sqlite, _connectionString);
            services.AddSqliteProvider();
            if (overrideDisplayAlias)
                services.AddSingleton<IEntityMappingResolver>(provider => new DisplayAliasResolver(
                    new DefaultEntityMappingResolver(provider.GetRequiredService<IEntityModelMetadataProvider>())));
            Services = services.BuildServiceProvider();

            using var executor = CreateExecutor();
            executor.ExecuteSql("CREATE TABLE returning_projection (Id INTEGER PRIMARY KEY, Counter INTEGER NOT NULL)");
            executor.ExecuteSql("INSERT INTO returning_projection (Id, Counter) VALUES (1, 10)");
        }

        internal ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();

        internal int ReadCounter()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Counter FROM returning_projection WHERE Id = 1";
            return checked((int)(long)command.ExecuteScalar());
        }

        public void Dispose()
        {
            Services.Dispose();
            if (File.Exists(_path)) File.Delete(_path);
        }
    }

    private sealed class DisplayAliasResolver(IEntityMappingResolver inner) : IEntityMappingResolver
    {
        public EntityDescriptor GetDescriptor(Type type) => inner.GetDescriptor(type);

        public EntityMappingMetadata Resolve(Type type, DatabaseContext context)
        {
            var mapping = inner.Resolve(type, context);
            if (type != typeof(ReturningDto)) return mapping;
            var columns = mapping.Columns.ToDictionary(pair => pair.Key, pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            var display = columns[nameof(ReturningDto.DisplayName)];
            columns[nameof(ReturningDto.DisplayName)] = new ColumnMappingMetadata
            {
                PropertyName = display.PropertyName,
                ColumnName = "final_display_alias",
                ClrType = display.ClrType,
                ConverterKind = display.ConverterKind,
                StorageKind = display.StorageKind
            };
            return new EntityMappingMetadata
            {
                EntityType = mapping.EntityType,
                Model = mapping.Model,
                MappingProfile = mapping.MappingProfile,
                Table = mapping.Table,
                TableRouteKey = mapping.TableRouteKey,
                Columns = columns
            };
        }
    }
}
