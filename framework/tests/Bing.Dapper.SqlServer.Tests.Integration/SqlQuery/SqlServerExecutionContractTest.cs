using System.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQL Server 参数、流式、取消和事务合同测试。
/// </summary>
[Collection(SqlServerIntegrationDatabaseCollection.Name)]
public sealed class SqlServerExecutionContractTest : IAsyncLifetime
{
    private readonly SqlServerIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化 SQL Server 合同测试。
    /// </summary>
    /// <param name="fixture">SQL Server 集成测试数据库固定装置。</param>
    public SqlServerExecutionContractTest(SqlServerIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 每个测试前清空合同表。
    /// </summary>
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <summary>
    /// 测试类结束时无需额外清理。
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 测试目的：SQL Server 应保留 decimal、datetime2 和显式 null 参数，并按实体类型完整物化结果。
    /// </summary>
    [IntegrationFact("SqlServer")]
    [Trait("Category", "Integration")]
    [Trait("Database", "SqlServer")]
    public async Task Query_WhenTypedAndNullParametersAreBound_ShouldMaterializeValues()
    {
        // Arrange
        var createdAt = new DateTime(2026, 9, 3, 12, 30, 15, 123, DateTimeKind.Unspecified);
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlContractIntegration(Code,NullableValue,Amount,CreatedAt,Enabled) " +
            "Values(@code,@nullableValue,@amount,@createdAt,@enabled)",
            new { code = "typed-row", nullableValue = (int?)null, amount = 12.3456m, createdAt, enabled = true });

        // Act
        using var query = _fixture.CreateQuery();
        var rows = await query.Sql(
                "Select Code,NullableValue,Amount,CreatedAt,Enabled From dbo.BingSqlContractIntegration " +
                "Where Code=@code", new { code = "typed-row" })
            .ToListAsync<ContractRow>();

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal("typed-row", row.Code);
        Assert.Null(row.NullableValue);
        Assert.Equal(12.3456m, row.Amount);
        Assert.Equal(createdAt, row.CreatedAt);
        Assert.True(row.Enabled);
    }

    /// <summary>
    /// 测试目的：SQL Server 异步流式查询应返回全部合同记录，预取消应在执行前传播。
    /// </summary>
    [IntegrationFact("SqlServer")]
    [Trait("Category", "Integration")]
    [Trait("Database", "SqlServer")]
    public async Task Query_WhenStreamingAndPreCancelled_ShouldReturnRowsAndCancel()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlContractIntegration(Code,NullableValue,Amount,CreatedAt,Enabled) " +
            "Values(@code,@nullableValue,@amount,@createdAt,@enabled)",
            new { code = "stream-row", nullableValue = (int?)7, amount = 1.25m,
                createdAt = new DateTime(2026, 9, 3), enabled = true });
        using var query = _fixture.CreateQuery();
        var description = query.Sql("Select Code,NullableValue,Amount,CreatedAt,Enabled " +
            "From dbo.BingSqlContractIntegration Order By Id");

        // Act
        var streamedRows = new List<ContractRow>();
        await foreach (var row in description.AsAsyncEnumerable<ContractRow>())
            streamedRows.Add(row);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Assert
        Assert.Single(streamedRows);
        Assert.Equal("stream-row", streamedRows[0].Code);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => description
            .ToListAsync<ContractRow>(cancellationToken: cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：SQL Server 事务应在提交后可见，并在回滚后保持数据不可见。
    /// </summary>
    [IntegrationFact("SqlServer")]
    [Trait("Category", "Integration")]
    [Trait("Database", "SqlServer")]
    public async Task Transaction_WhenCompletedOrRolledBack_ShouldKeepExpectedVisibility()
    {
        // Arrange
        var transactionScopeFactory = _fixture.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
        var committedCode = "committed-row";
        var rolledBackCode = "rolled-back-row";

        // Act
        using (var scope = transactionScopeFactory.Begin())
        using (var executor = scope.CreateExecutor())
        {
            await executor.ExecuteSqlAsync(
                "Insert Into dbo.BingSqlContractIntegration(Code,NullableValue,Amount,CreatedAt,Enabled) " +
                "Values(@code,@nullableValue,@amount,@createdAt,@enabled)",
                new { code = committedCode, nullableValue = (int?)null, amount = 2m,
                    createdAt = new DateTime(2026, 9, 3), enabled = true });
            scope.Commit();
        }

        using (var scope = transactionScopeFactory.Begin())
        using (var executor = scope.CreateExecutor())
        {
            await executor.ExecuteSqlAsync(
                "Insert Into dbo.BingSqlContractIntegration(Code,NullableValue,Amount,CreatedAt,Enabled) " +
                "Values(@code,@nullableValue,@amount,@createdAt,@enabled)",
                new { code = rolledBackCode, nullableValue = (int?)null, amount = 3m,
                    createdAt = new DateTime(2026, 9, 3), enabled = true });
            scope.Rollback();
        }

        // Assert
        using var query = _fixture.CreateQuery();
        var rows = await query.Sql(
                "Select Code From dbo.BingSqlContractIntegration Where Code In (@committedCode,@rolledBackCode)",
                new { committedCode, rolledBackCode })
            .ToListAsync<CodeRow>();
        Assert.Equal(new[] { committedCode }, rows.Select(row => row.Code));
    }

    /// <summary>
    /// 测试目的：SQL Server 事务作用域未完成时释放应自动回滚，避免遗漏提交导致脏数据。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task Transaction_WhenScopeIsDisposedWithoutCompletion_ShouldRollbackChanges()
    {
        // Arrange
        var transactionScopeFactory = _fixture.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
        using (var scope = transactionScopeFactory.Begin())
        using (var executor = scope.CreateExecutor())
        {
            await executor.ExecuteSqlAsync(
                "Insert Into dbo.BingSqlContractIntegration(Code,NullableValue,Amount,CreatedAt,Enabled) " +
                "Values(@code,@nullableValue,@amount,@createdAt,@enabled)",
                new { code = "implicit-rollback", nullableValue = (int?)null, amount = 4m,
                    createdAt = new DateTime(2026, 9, 3), enabled = true });
        }

        // Act
        using var query = _fixture.CreateQuery();
        var count = await query.Sql(
                "Select Count(*) From dbo.BingSqlContractIntegration Where Code=@code",
                new { code = "implicit-rollback" })
            .ScalarAsync<int>();

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// 测试目的：SQL Server 实体批量 Insert、Update、Delete 应真实修改受控表并返回准确影响行数。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task BatchCrud_WhenEntitiesAreProvided_ShouldPersistAndMutateRows()
    {
        // Arrange
        var rows = new[]
        {
            new BatchRow { Code = "batch-first", Name = "before" },
            new BatchRow { Code = "batch-second", Name = "before" },
            new BatchRow { Code = "batch-third", Name = "before" }
        };
        using var executor = _fixture.CreateExecutor();

        // Act
        var inserted = await executor.InsertBatchAsync(rows, new SqlBatchInsertOptions
        {
            BatchSize = 2,
            UseTransaction = true
        });
        using var idQuery = _fixture.CreateQuery();
        var ids = await idQuery.Sql(
                "Select Id From dbo.BingSqlBatchIntegration Where Code In (@first,@second,@third) Order By Code",
                new { first = "batch-first", second = "batch-second", third = "batch-third" })
            .ToListAsync<CodeIdRow>();
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index].Id = ids[index].Id;
            rows[index].Name = "after";
        }
        var updated = await executor.UpdateBatchAsync(rows, new SqlBatchUpdateOptions
        {
            BatchSize = 2,
            UseTransaction = true,
            UpdateOptions = new SqlUpdateOptions { IncludeProperties = new[] { nameof(BatchRow.Name) } }
        });
        var deleted = await executor.DeleteBatchAsync(rows, new SqlBatchDeleteOptions
        {
            BatchSize = 2,
            UseTransaction = true
        });

        // Assert
        Assert.Equal(3, inserted);
        Assert.Equal(3, updated);
        Assert.Equal(3, deleted);
        using var countQuery = _fixture.CreateQuery();
        Assert.Equal(0, await countQuery.Sql("Select Count(*) From dbo.BingSqlBatchIntegration")
            .ScalarAsync<int>());
    }

    /// <summary>
    /// 测试目的：SQL Server 批量事务在后续批次发生唯一键冲突时应回滚先前批次，禁止部分提交。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task InsertBatch_WhenLaterBatchFails_ShouldRollbackEarlierBatches()
    {
        // Arrange
        var rows = new[]
        {
            new BatchRow { Code = "duplicate-first", Name = "first" },
            new BatchRow { Code = "duplicate-first", Name = "second" }
        };
        using var executor = _fixture.CreateExecutor();

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => executor.InsertBatchAsync(rows,
            new SqlBatchInsertOptions { BatchSize = 1, UseTransaction = true }));

        // Assert
        using var query = _fixture.CreateQuery();
        Assert.Equal(0, await query.Sql("Select Count(*) From dbo.BingSqlBatchIntegration")
            .ScalarAsync<int>());
    }

    /// <summary>
    /// 测试目的：SQL Server IN 参数数量为 2098 时应成功执行，作为 2100 参数上限的公开入口控制边界。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task Query_WhenInParameterCountIs2098_ShouldExecuteSuccessfully()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlBatchIntegration(Code,Name) Values(@code,@name)",
            new { code = "in-2098", name = "boundary" });
        using var query = _fixture.CreateQuery();
        var id = await query.Sql(
                "Select Id From dbo.BingSqlBatchIntegration Where Code=@code",
                new { code = "in-2098" })
            .ScalarAsync<int>();
        var ids = Enumerable.Repeat(0, 2097).Prepend(id).ToArray();

        // Act
        var count = await query.Sql(
                "Select Count(*) From dbo.BingSqlBatchIntegration Where Id In @ids",
                new { ids })
            .ScalarAsync<int>();

        // Assert
        Assert.Equal(1, count);
    }

    /// <summary>
    /// 测试目的：SQL Server IN 参数数量为 2099 时应由真实 Provider 拒绝，记录公开 Dapper IN 展开的额外参数开销。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task Query_WhenInParameterCountIs2099_ShouldRejectAtSqlServerLimit()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlBatchIntegration(Code,Name) Values(@code,@name)",
            new { code = "in-2099", name = "boundary" });
        using var query = _fixture.CreateQuery();
        var id = await query.Sql(
                "Select Id From dbo.BingSqlBatchIntegration Where Code=@code",
                new { code = "in-2099" })
            .ScalarAsync<int>();
        var ids = Enumerable.Repeat(0, 2098).Prepend(id).ToArray();

        // Act
        var exception = await Record.ExceptionAsync(() => query.Sql(
                "Select Count(*) From dbo.BingSqlBatchIntegration Where Id In @ids",
                new { ids })
            .ScalarAsync<int>());

        // Assert
        Assert.NotNull(exception);
        Assert.Contains("2100", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试目的：SQL Server IN 参数数量为 2100 时应由真实 Provider 拒绝，验证边界不会被错误声明为可执行。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task Query_WhenInParameterCountIs2100_ShouldRejectAtSqlServerLimit()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlBatchIntegration(Code,Name) Values(@code,@name)",
            new { code = "in-2100", name = "boundary" });
        using var query = _fixture.CreateQuery();
        var id = await query.Sql(
                "Select Id From dbo.BingSqlBatchIntegration Where Code=@code",
                new { code = "in-2100" })
            .ScalarAsync<int>();
        var ids = Enumerable.Repeat(0, 2099).Prepend(id).ToArray();

        // Act
        var exception = await Record.ExceptionAsync(() => query.Sql(
                "Select Count(*) From dbo.BingSqlBatchIntegration Where Id In @ids",
                new { ids })
            .ScalarAsync<int>());
        
        // Assert
        Assert.NotNull(exception);
        Assert.Contains("2100", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试目的：SQL Server IN 参数数量为 2101 时应明确失败，且失败后同一查询对象仍可执行恢复查询。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task Query_WhenInParameterCountIs2101_ShouldRejectAndRemainReusable()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlBatchIntegration(Code,Name) Values(@code,@name)",
            new { code = "in-2101", name = "boundary" });
        using var query = _fixture.CreateQuery();
        var id = await query.Sql(
                "Select Id From dbo.BingSqlBatchIntegration Where Code=@code",
                new { code = "in-2101" })
            .ScalarAsync<int>();
        var ids = Enumerable.Repeat(0, 2100).Prepend(id).ToArray();

        // Act
        var exception = await Record.ExceptionAsync(() => query.Sql(
                "Select Count(*) From dbo.BingSqlBatchIntegration Where Id In @ids",
                new { ids })
            .ScalarAsync<int>());
        var count = await query.Sql(
                "Select Count(*) From dbo.BingSqlBatchIntegration Where Code=@code",
                new { code = "in-2101" })
            .ScalarAsync<int>();

        // Assert
        Assert.NotNull(exception);
        Assert.Contains("2100", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, count);
    }

    /// <summary>
    /// 测试目的：SQL Server 存储过程应返回 Output、InputOutput 和 ReturnValue，并物化过程结果集。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task ExecuteProcedureAsync_WhenOutputDirectionsAreConfigured_ShouldReturnValuesAndRows()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        var parameters = new SqlParameterCollection()
            .Add("InputValue", 3, DbType.Int32)
            .Add(new SqlParam("InputOutputValue", 5, DbType.Int32, ParameterDirection.InputOutput))
            .AddOutput("OutputValue", DbType.Int32)
            .Add(new SqlParam("ReturnValue", null, DbType.Int32, ParameterDirection.ReturnValue));

        // Act
        var result = await executor.ExecuteProcedureAsync("dbo.BingSqlContractProcedure", parameters);

        // Assert
        Assert.Equal(-1, result.Result);
        Assert.Equal(8, result.OutputParameters.GetValue<int>("InputOutputValue"));
        Assert.Equal(4, result.OutputParameters.GetValue<int>("OutputValue"));
        Assert.Equal(5, result.OutputParameters.GetValue<int>("ReturnValue"));
    }

    /// <summary>
    /// 测试目的：SQL Server 存储过程异步入口在预取消时应在创建命令前直接取消。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task ExecuteProcedureAsync_WhenCancellationIsRequested_ShouldCancelBeforeExecution()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteProcedureAsync(
            "dbo.BingSqlContractProcedure", cancellationToken: cancellationTokenSource.Token));
    }

    /// <summary>
    /// 测试目的：SQL Server 多结果集应按顺序读取，并在首个结果读取后提前释放时允许同一执行器再次使用。
    /// </summary>
    [IntegrationFact("SqlServer")]
    public async Task ExecuteMultiple_WhenFirstResultIsReadAndDisposedEarly_ShouldReleaseResources()
    {
        // Arrange
        using var seedExecutor = _fixture.CreateExecutor();
        await seedExecutor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlBatchIntegration(Code,Name) Values(@code,@name)",
            new { code = "multiple-first", name = "first" });
        await seedExecutor.ExecuteSqlAsync(
            "Insert Into dbo.BingSqlBatchIntegration(Code,Name) Values(@code,@name)",
            new { code = "multiple-second", name = "second" });
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = executor.CreateBatch()
            .Append("Select Code From dbo.BingSqlBatchIntegration Order By Code")
            .Append("Select Count(*) From dbo.BingSqlBatchIntegration")
            .Build();

        // Act
        using (var result = executor.Execute(command))
            Assert.Equal(new[] { "multiple-first", "multiple-second" }, result.Read<CodeRow>().Select(row => row.Code));
        using var nextResult = await executor.ExecuteAsync(command);
        var rows = await nextResult.ReadAsync<CodeRow>(CancellationToken.None);
        var count = await nextResult.ReadAsync<int>(CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "multiple-first", "multiple-second" }, rows.Select(row => row.Code));
        Assert.Equal(2, count.Single());
    }

    private sealed class ContractRow
    {
        public string Code { get; set; }
        public int? NullableValue { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Enabled { get; set; }
    }

    private sealed class CodeRow
    {
        public string Code { get; set; }
    }

    [Table("BingSqlBatchIntegration", Schema = "dbo")]
    private sealed class BatchRow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public bool Enabled { get; set; } = true;
    }

    private sealed class CodeIdRow
    {
        public int Id { get; set; }
    }
}