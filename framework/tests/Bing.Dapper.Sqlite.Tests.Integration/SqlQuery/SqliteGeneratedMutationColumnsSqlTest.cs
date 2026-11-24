using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
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
using Bing.Exceptions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证多列筛选的生成式 SQLite Mutation SQL 及其 Returning 执行。</summary>
public sealed class SqliteGeneratedMutationColumnsSqlTest
{
    private const string TableName = "generated_mutation_columns";
    private const string Projection = " Returning `Id` As `Id`, `Name` As `Name`, `Quantity` As `Quantity`, `Version` As `Version`";
    private const string InsertSql = "Insert Into `generated_mutation_columns` (`Name`, `Quantity`) Values (@_p_0, @_p_1)";
    private const string UpdateSql = "Update `generated_mutation_columns` Set `Name` = @_p_0, `Quantity` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3";
    private const string InsertReturningSql = InsertSql + Projection;
    private const string UpdateReturningSql = UpdateSql + Projection;

    private static readonly string[] IncludedProperties = { nameof(Row.Name), nameof(Row.Quantity) };
    private static readonly string[] ReversedIncludedProperties = { nameof(Row.Quantity), nameof(Row.Name) };

    /// <summary>多列筛选的非 Returning Insert 和 Update 使用完整静态 SQL 与有序参数执行。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredMutation_ShouldExecuteGeneratedInsertAndUpdate(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var insertRow = new Row { Name = "before", Quantity = 3, Version = 99 };
        var insert = host.Commands.Insert(insertRow, new SqlInsertOptions
        {
            IncludeProperties = ReversedIncludedProperties
        });

        AssertCommand(insert, InsertSql, SqlOperationKind.InsertValues, hasReturning: false,
            expectedParameters: new object[] { "before", 3 });
        Assert.Equal(1, await ExecuteWrite(executor, insert, asynchronous));
        Assert.Equal(1, host.ColumnSqlFactoryCallCount);

        var saved = Assert.Single(host.ReadRows());
        Assert.True(saved.Id > 0);
        Assert.Equal(1, saved.Version);

        var updateRow = new Row { Id = saved.Id, Name = "after", Quantity = 4, Version = 88 };
        var update = host.Commands.Update(updateRow, CreateUpdateOptions(originalVersion: 1));
        AssertCommand(update, UpdateSql, SqlOperationKind.Update, hasReturning: false,
            expectedParameters: new object[] { "after", 4, saved.Id, 1 });
        Assert.Equal(1, await ExecuteWrite(executor, update, asynchronous));
        Assert.Equal(2, host.ColumnSqlFactoryCallCount);

        var updated = Assert.Single(host.ReadRows());
        Assert.Equal("after", updated.Name);
        Assert.Equal(4, updated.Quantity);
        Assert.Equal(1, updated.Version);
    }

    /// <summary>多行筛选列 Insert 按行优先绑定，并保留数据库默认列值。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FilteredCombinedInsert_ShouldExecuteSelectedColumns(bool asynchronous, bool exclude)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var rows = new[]
        {
            new Row { Name = "first", Quantity = 3, Version = 91 },
            new Row { Name = "second", Quantity = 5, Version = 92 },
            new Row { Name = "third", Quantity = 8, Version = 93 }
        };
        var options = exclude
            ? new SqlInsertOptions { ExcludeProperties = new[] { nameof(Row.Version) } }
            : new SqlInsertOptions { IncludeProperties = ReversedIncludedProperties };
        var command = ((ISqlCombinedInsertMutationBuilder)host.Commands).InsertCombined(rows, options);
        var expectedSql = CreateBatchInsertSql(rows.Length);

        AssertCommand(command, expectedSql, SqlOperationKind.InsertValues, hasReturning: false,
            expectedParameters: new object[] { "first", 3, "second", 5, "third", 8 });
        Assert.Equal(Enumerable.Range(0, rows.Length * 2)
            .Select(index => "@_p_" + index.ToString(CultureInfo.InvariantCulture)),
            command.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(1, host.ColumnSqlFactoryCallCount);
        Assert.Equal(rows.Length, await ExecuteWrite(executor, command, asynchronous));

        var saved = host.ReadRows();
        Assert.Equal(rows.Length, saved.Length);
        Assert.Equal(new[] { "first", "second", "third" }, saved.Select(row => row.Name));
        Assert.Equal(new[] { 3, 5, 8 }, saved.Select(row => row.Quantity));
        Assert.All(saved, row => Assert.Equal(1, row.Version));
    }

    /// <summary>多行筛选 Insert 参与外层事务，回滚由事务所有者完成。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredCombinedInsert_InSharedTransaction_ShouldRollbackByOwner(bool asynchronous)
    {
        using var host = new TestHost();
        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        var rows = new[]
        {
            new Row { Name = "temporary-a", Quantity = 1 },
            new Row { Name = "temporary-b", Quantity = 2 }
        };
        var command = ((ISqlCombinedInsertMutationBuilder)host.Commands).InsertCombined(rows,
            new SqlInsertOptions { IncludeProperties = IncludedProperties });

        Assert.Equal(CreateBatchInsertSql(rows.Length), command.Sql);
        Assert.Equal(2, await ExecuteWrite(executor, command, asynchronous));
        await transaction.RollbackAsync();

        Assert.Empty(host.ReadRows());
        Assert.Equal(1, host.ColumnSqlFactoryCallCount);
    }

    /// <summary>多行 Insert 任一行违反约束时整条 SQLite 语句失败且不留下部分数据。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredCombinedInsert_WhenRowViolatesConstraint_ShouldLeaveNoRows(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var rows = new[]
        {
            new Row { Name = "valid", Quantity = 1 },
            new Row { Name = null, Quantity = 2 }
        };
        var command = ((ISqlCombinedInsertMutationBuilder)host.Commands).InsertCombined(rows,
            new SqlInsertOptions { IncludeProperties = IncludedProperties });

        Assert.Equal(CreateBatchInsertSql(rows.Length), command.Sql);
        if (asynchronous)
            await Assert.ThrowsAsync<SqliteException>(() => executor.ExecuteWriteAsync(command));
        else
            Assert.Throws<SqliteException>(() => executor.ExecuteWrite(command));

        Assert.Empty(host.ReadRows());
        Assert.Equal(1, host.ColumnSqlFactoryCallCount);
    }

    /// <summary>筛选写入两列时 InsertReturning 和 UpdateReturning 仍返回整行。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredReturning_ShouldExecuteAndReturnAllColumns(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        var insert = host.Commands.InsertReturning(new Row
        {
            Name = "created",
            Quantity = 5,
            Version = 99
        }, new SqlInsertOptions { IncludeProperties = ReversedIncludedProperties });

        AssertCommand(insert, InsertReturningSql, SqlOperationKind.InsertValues,
            hasReturning: true, expectedParameters: new object[] { "created", 5 });
        var inserted = Assert.Single(await ExecuteReturning(executor, insert, asynchronous));
        Assert.True(inserted.Id > 0);
        Assert.Equal("created", inserted.Name);
        Assert.Equal(5, inserted.Quantity);
        Assert.Equal(1, inserted.Version);
        Assert.Equal(1, host.ColumnSqlFactoryCallCount);

        inserted.Name = "updated";
        inserted.Quantity = 8;
        inserted.Version = 77;
        var update = host.Commands.UpdateReturning(inserted, CreateUpdateOptions(originalVersion: 1));
        AssertCommand(update, UpdateReturningSql, SqlOperationKind.Update,
            hasReturning: true, expectedParameters: new object[] { "updated", 8, inserted.Id, 1 });

        var updated = Assert.Single(await ExecuteReturning(executor, update, asynchronous));
        Assert.Equal(inserted.Id, updated.Id);
        Assert.Equal("updated", updated.Name);
        Assert.Equal(8, updated.Quantity);
        Assert.Equal(1, updated.Version);
        Assert.Equal(2, host.ColumnSqlFactoryCallCount);
    }

    /// <summary>UpdateReturning 使用 stale Original 版本时抛出并发异常且保留原行。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredReturning_WhenOriginalVersionIsStale_ShouldThrow(bool asynchronous)
    {
        using var host = new TestHost();
        using var executor = host.CreateExecutor();
        host.Seed(new Row { Id = 7, Name = "stable", Quantity = 6, Version = 1 });
        var command = host.Commands.UpdateReturning(new Row
        {
            Id = 7,
            Name = "stale",
            Quantity = 9,
            Version = 1
        }, CreateUpdateOptions(originalVersion: 0));

        AssertCommand(command, UpdateReturningSql, SqlOperationKind.Update,
            hasReturning: true, expectedParameters: new object[] { "stale", 9, 7, 0 });
        if (asynchronous)
            await Assert.ThrowsAsync<ConcurrencyException>(() => executor.ExecuteReturningAsync<Row>(command));
        else
            Assert.Throws<ConcurrencyException>(() => executor.ExecuteReturning<Row>(command));

        Assert.Equal(1, host.ColumnSqlFactoryCallCount);
        var unchanged = Assert.Single(host.ReadRows());
        Assert.Equal("stable", unchanged.Name);
        Assert.Equal(6, unchanged.Quantity);
        Assert.Equal(1, unchanged.Version);
    }

    /// <summary>共享事务中的筛选 Returning 更新由外层回滚并恢复数据库原值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredReturning_InSharedTransaction_ShouldRollbackByOwner(bool asynchronous)
    {
        using var host = new TestHost();
        host.Seed(new Row { Id = 13, Name = "original", Quantity = 2, Version = 1 });

        await using var transaction = await host.Services.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        using var executor = transaction.CreateExecutor();
        var command = host.Commands.UpdateReturning(new Row
        {
            Id = 13,
            Name = "temporary",
            Quantity = 10,
            Version = 1
        }, CreateUpdateOptions(originalVersion: 1));

        AssertCommand(command, UpdateReturningSql, SqlOperationKind.Update,
            hasReturning: true, expectedParameters: new object[] { "temporary", 10, 13, 1 });
        var updated = Assert.Single(await ExecuteReturning(executor, command, asynchronous));
        Assert.Equal("temporary", updated.Name);
        Assert.Equal(10, updated.Quantity);
        Assert.Equal(1, updated.Version);
        await transaction.RollbackAsync();

        Assert.Equal(1, host.ColumnSqlFactoryCallCount);
        var restored = Assert.Single(host.ReadRows());
        Assert.Equal(13, restored.Id);
        Assert.Equal("original", restored.Name);
        Assert.Equal(2, restored.Quantity);
        Assert.Equal(1, restored.Version);
    }

    private static SqlUpdateOptions<Row> CreateUpdateOptions(int originalVersion) =>
        new SqlUpdateOptions<Row> { IncludeProperties = ReversedIncludedProperties }
            .Original(row => row.Version, originalVersion);

    private static Task<int> ExecuteWrite(ISqlExecutor executor, SqlWriteCommand command, bool asynchronous) =>
        asynchronous ? executor.ExecuteWriteAsync(command) : Task.FromResult(executor.ExecuteWrite(command));

    private static Task<System.Collections.Generic.List<Row>> ExecuteReturning(ISqlExecutor executor,
        SqlWriteCommand command, bool asynchronous) => asynchronous
        ? executor.ExecuteReturningAsync<Row>(command)
        : Task.FromResult(executor.ExecuteReturning<Row>(command));

    /// <summary>构造测试所需行优先多行 SQL 和连续参数槽位。</summary>
    private static string CreateBatchInsertSql(int rowCount) =>
        "Insert Into `generated_mutation_columns` (`Name`, `Quantity`) Values " +
        string.Join(", ", Enumerable.Range(0, rowCount).Select(row =>
            "(@_p_" + (row * 2).ToString(CultureInfo.InvariantCulture) + ", @_p_" +
            (row * 2 + 1).ToString(CultureInfo.InvariantCulture) + ")"));

    private static void AssertCommand(SqlWriteCommand command, string sql, SqlOperationKind operation,
        bool hasReturning, object[] expectedParameters)
    {
        Assert.Equal(sql, command.Sql);
        Assert.Equal(operation, command.OperationKind);
        Assert.Equal(hasReturning, command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(expectedParameters, command.Parameters.Select(parameter => parameter.Value));
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public int Version { get; set; }
    }

    private EntityModelMetadata CreateModel() => new(typeof(Row), TableName, null,
        new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
                isKey: true, databaseGeneratedOption: DatabaseGeneratedOption.Identity,
                generatedGetter: static row => ((Row)row).Id,
                generatedSetter: static (row, value) => ((Row)row).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                generatedGetter: static row => ((Row)row).Name,
                generatedSetter: static (row, value) => ((Row)row).Name = (string)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Quantity), typeof(int), true, true,
                generatedGetter: static row => ((Row)row).Quantity,
                generatedSetter: static (row, value) => ((Row)row).Quantity = (int)value),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int), true, true,
                isConcurrencyToken: true,
                generatedGetter: static row => ((Row)row).Version,
                generatedSetter: static (row, value) => ((Row)row).Version = (int)value)
        }, static () => new Row())
    {
        GeneratedMutationColumnsSql = (provider, operation, properties, rowCount, returning) =>
        {
            ColumnSqlFactoryCallCount++;
            if (provider != "bing.sqlite" || properties == null || !properties.SequenceEqual(IncludedProperties) ||
                rowCount <= 0 || operation == SqlOperationKind.Update && rowCount != 1)
                return null;

            return (operation, returning) switch
            {
                (SqlOperationKind.InsertValues, false) when rowCount == 1 => InsertSql,
                (SqlOperationKind.Update, false) => UpdateSql,
                (SqlOperationKind.InsertValues, true) when rowCount == 1 => InsertReturningSql,
                (SqlOperationKind.Update, true) => UpdateReturningSql,
                (SqlOperationKind.InsertValues, false) => CreateBatchInsertSql(rowCount),
                (SqlOperationKind.InsertValues, true) => CreateBatchInsertSql(rowCount) + Projection,
                _ => null
            };
        }
    };

    private int ColumnSqlFactoryCallCount { get; set; }

    private sealed class TestHost : System.IDisposable
    {
        private readonly string _connectionString;
        internal ServiceProvider Services { get; }
        internal SqliteConnection Connection { get; }
        internal ISqlEntityMutationCommandBuilder Commands { get; }
        internal int ColumnSqlFactoryCallCount => _factory.ColumnSqlFactoryCallCount;

        private readonly SqliteGeneratedMutationColumnsSqlTest _factory;

        internal TestHost()
        {
            _factory = new SqliteGeneratedMutationColumnsSqlTest();
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = "generated_mutation_columns_" + System.Guid.NewGuid().ToString("N"),
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                Pooling = false
            }.ToString();

            var model = _factory.CreateModel();
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
            Connection.Execute("CREATE TABLE " + TableName +
                " (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Quantity INTEGER NOT NULL, Version INTEGER NOT NULL DEFAULT 1)");
        }

        internal ISqlExecutor CreateExecutor() => Services.GetRequiredService<ISqlExecutorFactory>().Create();

        internal void Seed(params Row[] rows)
        {
            foreach (var row in rows)
                Connection.Execute("INSERT INTO " + TableName + " (Id, Name, Quantity, Version) " +
                    "VALUES (@Id, @Name, @Quantity, @Version)", row);
        }

        internal Row[] ReadRows() => Connection.Query<Row>("SELECT Id, Name, Quantity, Version FROM " +
            TableName + " ORDER BY Id").ToArray();

        public void Dispose()
        {
            Services.Dispose();
            Connection.Dispose();
        }
    }
}
