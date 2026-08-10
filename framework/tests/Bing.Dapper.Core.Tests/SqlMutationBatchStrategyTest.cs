using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Mutations;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// Mutation 批量自动策略测试。
/// </summary>
public sealed class SqlMutationBatchStrategyTest
{
    /// <summary>
    /// 测试目的：Provider 显式支持多行 Values 时，Auto 策略应为同一分片生成一条组合 Insert 命令。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenAutoStrategyAndProviderSupportsMultiRowValues_ShouldExecuteCombinedCommand()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: true);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" }
        }, new SqlBatchInsertOptions { BatchSize = 2, UseTransaction = false });

        // Assert
        var command = Assert.Single(executor.Commands);
        Assert.Equal(2, affectedRows);
        Assert.Equal("Insert Into [mutation_samples] ([Name]) Values (@_p_0), (@_p_1)", command.Sql);
        Assert.Equal(new object[] { "first", "second" }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：Provider 未声明多行 Values 支持时，Auto 策略应稳定回退为逐实体 Insert 命令。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenAutoStrategyAndProviderDoesNotSupportMultiRowValues_ShouldExecutePerEntityCommands()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" }
        }, new SqlBatchInsertOptions { BatchSize = 2, UseTransaction = false });

        // Assert
        Assert.Equal(2, affectedRows);
        Assert.Equal(2, executor.Commands.Count);
        Assert.All(executor.Commands, command =>
            Assert.Equal("Insert Into [mutation_samples] ([Name]) Values (@_p_0)", command.Sql));
    }

    /// <summary>
    /// 测试目的：PerEntity 应按单条数据库命令校验参数数和 SQL 长度，不能累计同一执行分组中的独立命令。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenPerEntityCommandsIndividuallyFitLimits_ShouldExecuteAllCommands()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false, maxParameterCount: 1);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" }
        }, new SqlBatchInsertOptions
        {
            BatchSize = 2,
            MaxSqlLength = 58,
            Strategy = SqlBatchInsertStrategy.PerEntity,
            UseTransaction = false
        });

        // Assert
        Assert.Equal(2, affectedRows);
        Assert.Equal(2, executor.Commands.Count);
        Assert.All(executor.Commands, command =>
        {
            Assert.Single(command.Parameters);
            Assert.True(command.Sql.Length <= 58);
        });
    }

    /// <summary>
    /// 测试目的：Auto DeleteBatch 应将无并发列的单主键实体合并为一条参数化 IN 删除命令。
    /// </summary>
    [Fact]
    public void DeleteBatch_WhenAutoStrategyAndSingleKeyHasNoConcurrency_ShouldExecuteCombinedCommand()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.DeleteBatch(new[]
        {
            new DeleteSample { Id = 1 },
            new DeleteSample { Id = 2 }
        }, new SqlBatchDeleteOptions { BatchSize = 2, UseTransaction = false });

        // Assert
        var command = Assert.Single(executor.Commands);
        Assert.Equal(2, affectedRows);
        Assert.Equal("Delete From [delete_samples] Where [Id] In (@_p_0,@_p_1)", command.Sql);
        Assert.Equal(new object[] { 1, 2 }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：带并发列的 Combined Delete 影响多行时应按批次实体数校验，不能套用单实体一行规则。
    /// </summary>
    [Fact]
    public void DeleteBatch_WhenCombinedConcurrencyCommandAffectsAllRows_ShouldReturnAffectedRows()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite)
        {
            AffectedRows = () => 2
        };

        // Act
        var affectedRows = executor.DeleteBatch(new[]
        {
            new ConcurrencyDeleteSample { Id = 1, Version = "v1" },
            new ConcurrencyDeleteSample { Id = 2, Version = "v2" }
        }, new SqlBatchDeleteOptions { BatchSize = 2, UseTransaction = false });

        // Assert
        Assert.Equal(2, affectedRows);
        Assert.Single(executor.Commands);
    }

    /// <summary>
    /// 测试目的：带并发列的 Combined Delete 少删一行时应在批次层抛出准确的 Delete 并发异常。
    /// </summary>
    [Fact]
    public async Task DeleteBatchAsync_WhenCombinedConcurrencyCommandAffectsFewerRows_ShouldThrowConcurrencyException()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite)
        {
            AffectedRows = () => 1
        };

        // Act
        var exception = await Assert.ThrowsAsync<Bing.Exceptions.ConcurrencyException>(() => executor.DeleteBatchAsync(
            new[]
            {
                new ConcurrencyDeleteSample { Id = 1, Version = "v1" },
                new ConcurrencyDeleteSample { Id = 2, Version = "v2" }
            }, new SqlBatchDeleteOptions { BatchSize = 2, UseTransaction = false }));

        // Assert
        Assert.Contains("批量 Delete 预期影响 2 行，实际影响 1 行。", exception.Message);
        Assert.Single(executor.Commands);
    }

    /// <summary>
    /// 测试目的：未注册 Provider 优化 Update 渲染器时，显式优化策略必须返回明确异常，不能静默降级。
    /// </summary>
    [Fact]
    public void UpdateBatch_WhenProviderOptimizedStrategyHasNoRenderer_ShouldThrowNotSupportedException()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.UpdateBatch(new[]
        {
            new UpdateSample { Id = 1, Name = "updated" }
        }, new SqlBatchUpdateOptions
        {
            Strategy = SqlBatchUpdateStrategy.ProviderOptimized,
            UseTransaction = false
        }));

        // Assert
        Assert.Equal("Provider test.mutation-batch 未注册优化批量 Update 渲染器。", exception.Message);
        Assert.Empty(executor.Commands);
    }

    /// <summary>
    /// 测试目的：批量 Update 应接受强类型并发原始值，并将其写入实体条件参数。
    /// </summary>
    [Fact]
    public void UpdateBatch_WhenTypedOriginalValueIsProvided_ShouldUseConfiguredValue()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.UpdateBatch(new[]
        {
            new ConcurrencyUpdateSample { Id = 1, Name = "first", Version = "v1" }
        }, new SqlBatchUpdateOptions
        {
            UseTransaction = false,
            UpdateOptions = new SqlUpdateOptions<ConcurrencyUpdateSample>
            {
                ConcurrencyConflictBehavior = SqlConcurrencyConflictBehavior.ReturnAffectedRows
            }.Original(item => item.Version, "original")
        });

        // Assert
        Assert.Equal(4, affectedRows);
        Assert.Equal("original", executor.Commands.Single().Parameters.Last().Value);
    }

    /// <summary>
    /// 测试目的：批量 Delete 应接受强类型并发原始值，并将其写入实体条件参数。
    /// </summary>
    [Fact]
    public void DeleteBatch_WhenTypedOriginalValueIsProvided_ShouldUseConfiguredValue()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.DeleteBatch(new[]
        {
            new ConcurrencyDeleteSample { Id = 1, Version = "v1" }
        }, new SqlBatchDeleteOptions
        {
            UseTransaction = false,
            DeleteOptions = new SqlDeleteOptions<ConcurrencyDeleteSample>
            {
                ConcurrencyConflictBehavior = SqlConcurrencyConflictBehavior.ReturnAffectedRows
            }.Original(item => item.Version, "original")
        });

        // Assert
        Assert.Equal(2, affectedRows);
        Assert.Equal("original", executor.Commands.Single().Parameters.Last().Value);
    }

    /// <summary>
    /// 测试目的：带并发列的优化批量 Update 受影响行数少于实体数时，应抛出框架并发异常，避免静默丢失更新。
    /// </summary>
    [Fact]
    public void UpdateBatch_WhenOptimizedConcurrencyCommandAffectsFewerRows_ShouldThrowConcurrencyException()
    {
        // Arrange
        var renderer = new TestBatchUpdateRenderer();
        using var provider = CreateServiceProvider(supportsMultiRowValues: false, renderer: renderer);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var exception = Assert.Throws<Bing.Exceptions.ConcurrencyException>(() => executor.UpdateBatch(new[]
        {
            new ConcurrencyUpdateSample { Id = 1, Name = "first", Version = "v1" },
            new ConcurrencyUpdateSample { Id = 2, Name = "second", Version = "v2" }
        }, new SqlBatchUpdateOptions
        {
            Strategy = SqlBatchUpdateStrategy.ProviderOptimized,
            UseTransaction = false
        }));

        // Assert
        Assert.Contains("批量 Update 预期影响 2 行，实际影响 1 行。", exception.Message);
        Assert.Equal(2, renderer.LastEntityCount);
        Assert.Single(executor.Commands);
    }

    /// <summary>
    /// 测试目的：Auto 组合 Insert 应受 Provider 参数上限约束，将三条单参数实体按两个和一个实体分片。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenAutoStrategyExceedsProviderParameterLimit_ShouldSplitCombinedCommands()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: true, maxParameterCount: 2);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" },
            new MutationSample { Name = "third" }
        }, new SqlBatchInsertOptions { BatchSize = 3, UseTransaction = false });

        // Assert
        Assert.Equal(3, affectedRows);
        Assert.Equal(new[] { 2, 1 }, executor.Commands.Select(command => command.Parameters.Count));
        Assert.Equal("Insert Into [mutation_samples] ([Name]) Values (@_p_0), (@_p_1)", executor.Commands[0].Sql);
        Assert.Equal("Insert Into [mutation_samples] ([Name]) Values (@_p_0)", executor.Commands[1].Sql);
    }

    /// <summary>
    /// 测试目的：组合 Insert 应按最终 SQL 长度确定最大分片，不能因重复计算 Insert 前缀而过早拆分。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenAutoStrategyUsesSqlLengthLimit_ShouldSplitByRenderedSqlLength()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: true);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        const int maxSqlLength = 65;

        // Act
        var affectedRows = executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" },
            new MutationSample { Name = "third" }
        }, new SqlBatchInsertOptions
        {
            BatchSize = 3,
            MaxSqlLength = maxSqlLength,
            UseTransaction = false
        });

        // Assert
        Assert.Equal(3, affectedRows);
        Assert.Equal(new[] { 2, 1 }, executor.Commands.Select(command => command.Parameters.Count));
        Assert.All(executor.Commands, command => Assert.True(command.Sql.Length <= maxSqlLength));
        Assert.Equal("Insert Into [mutation_samples] ([Name]) Values (@_p_0), (@_p_1)", executor.Commands[0].Sql);
    }

    /// <summary>
    /// 测试目的：单实体生成的 SQL 已超过长度限制时，组合 Insert 应拒绝产生不可执行命令。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenSqlLengthCannotFitOneEntity_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: true);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" }
        }, new SqlBatchInsertOptions { MaxSqlLength = 53, UseTransaction = false }));

        // Assert
        Assert.Equal("当前 Provider 参数或 SQL 长度上限无法容纳一个 Mutation 实体。", exception.Message);
        Assert.Empty(executor.Commands);
    }

    /// <summary>
    /// 测试目的：空批量应在解析 Auto 策略前直接返回零，不能要求注册 Provider 或 Mutation Builder Factory。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenEntitySetIsEmpty_ShouldReturnZeroWithoutResolvingProvider()
    {
        // Arrange
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var executor = new RecordingExecutor(serviceProvider, DatabaseType.Sqlite);

        // Act
        var affectedRows = executor.InsertBatch(Array.Empty<MutationSample>());

        // Assert
        Assert.Equal(0, affectedRows);
        Assert.Empty(executor.Commands);
    }

    /// <summary>
    /// 测试目的：异步单实体 Insert 在调用前已取消时，应在创建命令前短路。
    /// </summary>
    [Fact]
    public async Task InsertAsync_WhenCancellationRequested_ShouldNotExecuteCommand()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: true);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.InsertAsync(
            new MutationSample { Name = "cancelled" }, cancellationToken: cancellationTokenSource.Token));
        Assert.Empty(executor.Commands);
        Assert.Empty(executor.CancellationTokens);
    }

    /// <summary>
    /// 测试目的：预取消必须先于单体 Mutation 和过程入口的 Provider、Builder 与写入能力校验执行。
    /// </summary>
    /// <param name="operation">待执行的 Mutation 操作类型。</param>
    [Theory]
    [InlineData(SingleMutationOperation.Insert)]
    [InlineData(SingleMutationOperation.Update)]
    [InlineData(SingleMutationOperation.Delete)]
    [InlineData(SingleMutationOperation.Procedure)]
    public async Task SingleMutationAsync_WhenCancellationRequested_ShouldCancelBeforeValidation(
        SingleMutationOperation operation)
    {
        // Arrange
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var executor = new RecordingExecutor(serviceProvider, DatabaseType.Sqlite);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExecuteSingleAsync(executor, operation,
            cancellationTokenSource.Token));
        Assert.Empty(executor.Commands);
        Assert.Empty(executor.CancellationTokens);
    }

    /// <summary>
    /// 测试目的：预取消的批量 Mutation 必须在枚举输入和构建 SQL 前直接取消。
    /// </summary>
    /// <param name="operation">批量 Mutation 操作类型。</param>
    [Theory]
    [InlineData(BatchMutationOperation.Insert)]
    [InlineData(BatchMutationOperation.Update)]
    [InlineData(BatchMutationOperation.Delete)]
    public async Task BatchMutationAsync_WhenCancellationRequested_ShouldNotEnumerateEntities(
        BatchMutationOperation operation)
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: true);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var entities = new ThrowOnEnumerationEnumerable<MutationSample>();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExecuteBatchAsync(executor, operation, entities,
            cancellationTokenSource.Token));
        Assert.Equal(0, entities.EnumerationCount);
        Assert.Empty(executor.Commands);
        Assert.Empty(executor.CancellationTokens);
    }

    /// <summary>
    /// 测试目的：无事务异步批量 Insert 在首条命令完成后取消时，应停止执行后续命令。
    /// </summary>
    [Fact]
    public async Task InsertBatchAsync_WhenCancelledAfterFirstCommandWithoutTransaction_ShouldStopRemainingCommands()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultiRowValues: false);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        using var cancellationTokenSource = new CancellationTokenSource();
        executor.AfterExecuteAsync = cancellationTokenSource.Cancel;

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.InsertBatchAsync(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" }
        }, new SqlBatchInsertOptions { UseTransaction = false }, cancellationToken: cancellationTokenSource.Token));
        Assert.Single(executor.Commands);
        Assert.Equal(cancellationTokenSource.Token, Assert.Single(executor.CancellationTokens));
    }

    /// <summary>
    /// 测试目的：事务型异步批量 Insert 取消后，应使用不可取消令牌回滚已开始的事务。
    /// </summary>
    [Fact]
    public async Task InsertBatchAsync_WhenCancelledWithTransaction_ShouldRollbackWithoutCancellationToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var transactionScope = new Mock<ISqlTransactionScope>();
        transactionScope.Setup(scope => scope.RollbackAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        var transactionScopeFactory = new Mock<ISqlTransactionScopeFactory>();
        using var provider = CreateServiceProvider(supportsMultiRowValues: false, transactionScopeFactory: transactionScopeFactory.Object);
        using var transactionExecutor = new RecordingExecutor(provider, DatabaseType.Sqlite)
        {
            AfterExecuteAsync = cancellationTokenSource.Cancel
        };
        transactionScope.Setup(scope => scope.CreateExecutor()).Returns(transactionExecutor);
        transactionScopeFactory.Setup(factory => factory.BeginAsync(null, cancellationTokenSource.Token))
            .ReturnsAsync(transactionScope.Object);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.InsertBatchAsync(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" }
        }, new SqlBatchInsertOptions { UseTransaction = true }, cancellationToken: cancellationTokenSource.Token));
        Assert.Single(transactionExecutor.Commands);
        transactionScope.Verify(scope => scope.RollbackAsync(CancellationToken.None), Times.Once);
        transactionScope.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 测试目的：最后一条批量命令完成后取消时，取消异常应优先于回滚异常保留。
    /// </summary>
    [Fact]
    public async Task InsertBatchAsync_WhenCancelledBeforeCommitAndRollbackFails_ShouldAggregateCancellationFirst()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var rollbackException = new InvalidOperationException("rollback failed");
        var transactionScope = new Mock<ISqlTransactionScope>();
        transactionScope.Setup(scope => scope.RollbackAsync(CancellationToken.None)).ThrowsAsync(rollbackException);
        var transactionScopeFactory = new Mock<ISqlTransactionScopeFactory>();
        using var provider = CreateServiceProvider(supportsMultiRowValues: false,
            transactionScopeFactory: transactionScopeFactory.Object);
        using var transactionExecutor = new RecordingExecutor(provider, DatabaseType.Sqlite)
        {
            AfterExecuteAsync = cancellationTokenSource.Cancel
        };
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        transactionScope.Setup(scope => scope.CreateExecutor()).Returns(transactionExecutor);
        transactionScopeFactory.Setup(factory => factory.BeginAsync(null, cancellationTokenSource.Token))
            .ReturnsAsync(transactionScope.Object);

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() => executor.InsertBatchAsync(new[]
        {
            new MutationSample { Name = "first" }
        }, new SqlBatchInsertOptions { UseTransaction = true }, cancellationToken: cancellationTokenSource.Token));

        // Assert
        Assert.IsType<OperationCanceledException>(exception.InnerExceptions[0]);
        Assert.Same(rollbackException, exception.InnerExceptions[1]);
        transactionScope.Verify(scope => scope.RollbackAsync(CancellationToken.None), Times.Once);
        transactionScope.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 测试目的：批量命令成功但同步提交失败时，应保留提交异常，不能再次回滚覆盖真实失败原因。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenCommitFails_ShouldPreserveCommitExceptionWithoutSecondRollback()
    {
        // Arrange
        var commitException = new InvalidOperationException("commit failed");
        var transactionScope = new Mock<ISqlTransactionScope>();
        var transactionScopeFactory = new Mock<ISqlTransactionScopeFactory>();
        using var provider = CreateServiceProvider(supportsMultiRowValues: false,
            transactionScopeFactory: transactionScopeFactory.Object);
        using var transactionExecutor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        transactionScope.Setup(scope => scope.CreateExecutor()).Returns(transactionExecutor);
        transactionScope.Setup(scope => scope.Commit()).Throws(commitException);
        transactionScopeFactory.Setup(factory => factory.Begin(null)).Returns(transactionScope.Object);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" }
        }, new SqlBatchInsertOptions { UseTransaction = true }));

        // Assert
        Assert.Same(commitException, exception);
        transactionScope.Verify(scope => scope.Rollback(), Times.Never);
    }

    /// <summary>
    /// 测试目的：批量命令成功但异步提交失败时，应保留提交异常，不能再次回滚覆盖真实失败原因。
    /// </summary>
    [Fact]
    public async Task InsertBatchAsync_WhenCommitFails_ShouldPreserveCommitExceptionWithoutSecondRollback()
    {
        // Arrange
        var commitException = new InvalidOperationException("commit failed");
        var transactionScope = new Mock<ISqlTransactionScope>();
        var transactionScopeFactory = new Mock<ISqlTransactionScopeFactory>();
        using var provider = CreateServiceProvider(supportsMultiRowValues: false,
            transactionScopeFactory: transactionScopeFactory.Object);
        using var transactionExecutor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        transactionScope.Setup(scope => scope.CreateExecutor()).Returns(transactionExecutor);
        transactionScope.Setup(scope => scope.CommitAsync(CancellationToken.None)).ThrowsAsync(commitException);
        transactionScopeFactory.Setup(factory => factory.BeginAsync(null, CancellationToken.None))
            .ReturnsAsync(transactionScope.Object);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.InsertBatchAsync(new[]
        {
            new MutationSample { Name = "first" }
        }, new SqlBatchInsertOptions { UseTransaction = true }));

        // Assert
        Assert.Same(commitException, exception);
        transactionScope.Verify(scope => scope.RollbackAsync(CancellationToken.None), Times.Never);
    }

    /// <summary>
    /// 测试目的：批量命令和同步回滚均失败时，应聚合原始命令异常和回滚异常，避免丢失执行诊断。
    /// </summary>
    [Fact]
    public void InsertBatch_WhenCommandAndRollbackFail_ShouldAggregateOriginalAndRollbackExceptions()
    {
        // Arrange
        var commandException = new InvalidOperationException("command failed");
        var rollbackException = new InvalidOperationException("rollback failed");
        var transactionScope = new Mock<ISqlTransactionScope>();
        var transactionScopeFactory = new Mock<ISqlTransactionScopeFactory>();
        using var provider = CreateServiceProvider(supportsMultiRowValues: false,
            transactionScopeFactory: transactionScopeFactory.Object);
        using var transactionExecutor = new RecordingExecutor(provider, DatabaseType.Sqlite)
        {
            ExecuteException = commandException
        };
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        transactionScope.Setup(scope => scope.CreateExecutor()).Returns(transactionExecutor);
        transactionScope.Setup(scope => scope.Rollback()).Throws(rollbackException);
        transactionScopeFactory.Setup(factory => factory.Begin(null)).Returns(transactionScope.Object);

        // Act
        var exception = Assert.Throws<AggregateException>(() => executor.InsertBatch(new[]
        {
            new MutationSample { Name = "first" }
        }, new SqlBatchInsertOptions { UseTransaction = true }));

        // Assert
        Assert.Equal(new Exception[] { commandException, rollbackException }, exception.Flatten().InnerExceptions);
        transactionScope.Verify(scope => scope.Rollback(), Times.Once);
        transactionScope.Verify(scope => scope.Commit(), Times.Never);
    }

    /// <summary>
    /// 测试目的：批量命令和异步回滚均失败时，应聚合原始命令异常和回滚异常，避免丢失执行诊断。
    /// </summary>
    [Fact]
    public async Task InsertBatchAsync_WhenCommandAndRollbackFail_ShouldAggregateOriginalAndRollbackExceptions()
    {
        // Arrange
        var commandException = new InvalidOperationException("command failed");
        var rollbackException = new InvalidOperationException("rollback failed");
        var transactionScope = new Mock<ISqlTransactionScope>();
        var transactionScopeFactory = new Mock<ISqlTransactionScopeFactory>();
        using var provider = CreateServiceProvider(supportsMultiRowValues: false,
            transactionScopeFactory: transactionScopeFactory.Object);
        using var transactionExecutor = new RecordingExecutor(provider, DatabaseType.Sqlite)
        {
            ExecuteAsyncException = commandException
        };
        using var executor = new RecordingExecutor(provider, DatabaseType.Sqlite);
        transactionScope.Setup(scope => scope.CreateExecutor()).Returns(transactionExecutor);
        transactionScope.Setup(scope => scope.RollbackAsync(CancellationToken.None)).ThrowsAsync(rollbackException);
        transactionScopeFactory.Setup(factory => factory.BeginAsync(null, CancellationToken.None))
            .ReturnsAsync(transactionScope.Object);

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() => executor.InsertBatchAsync(new[]
        {
            new MutationSample { Name = "first" }
        }, new SqlBatchInsertOptions { UseTransaction = true }));

        // Assert
        Assert.Equal(new Exception[] { commandException, rollbackException }, exception.Flatten().InnerExceptions);
        transactionScope.Verify(scope => scope.RollbackAsync(CancellationToken.None), Times.Once);
        transactionScope.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 创建已注册测试 Provider 的服务提供程序。
    /// </summary>
    /// <param name="supportsMultiRowValues">Provider 是否支持标准多行 Values。</param>
    /// <param name="maxParameterCount">可选的 Provider 单命令最大参数数量。</param>
    /// <param name="transactionScopeFactory">可选的事务作用域工厂测试替身。</param>
    /// <param name="renderer">可选的 Provider 优化批量 Update 渲染器。</param>
    /// <returns>用于执行器测试的服务提供程序。</returns>
    private static ServiceProvider CreateServiceProvider(bool supportsMultiRowValues, int? maxParameterCount = null,
        ISqlTransactionScopeFactory transactionScopeFactory = null, ISqlBatchUpdateRenderer renderer = null)
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSingleton<ISqlProvider>(new TestProvider(supportsMultiRowValues, maxParameterCount));
        if (renderer != null)
            services.AddSingleton<ISqlBatchUpdateRenderer>(renderer);
        if (transactionScopeFactory != null)
            services.AddSingleton(transactionScopeFactory);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 执行指定的异步批量 Mutation。
    /// </summary>
    /// <param name="executor">测试执行器。</param>
    /// <param name="operation">批量 Mutation 操作类型。</param>
    /// <param name="entities">实体序列。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步执行结果的任务。</returns>
    private static Task<int> ExecuteBatchAsync(RecordingExecutor executor, BatchMutationOperation operation,
        IEnumerable<MutationSample> entities, CancellationToken cancellationToken) => operation switch
    {
        BatchMutationOperation.Insert => executor.InsertBatchAsync(entities, cancellationToken: cancellationToken),
        BatchMutationOperation.Update => executor.UpdateBatchAsync(entities, cancellationToken: cancellationToken),
        BatchMutationOperation.Delete => executor.DeleteBatchAsync(entities, cancellationToken: cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    /// <summary>
    /// 执行指定的异步单体 Mutation 或存储过程。
    /// </summary>
    /// <param name="executor">测试执行器。</param>
    /// <param name="operation">Mutation 操作类型。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步执行结果的任务。</returns>
    private static Task ExecuteSingleAsync(RecordingExecutor executor, SingleMutationOperation operation,
        CancellationToken cancellationToken) => operation switch
    {
        SingleMutationOperation.Insert => executor.InsertAsync(new MutationSample { Name = "cancelled" },
            cancellationToken: cancellationToken),
        SingleMutationOperation.Update => executor.UpdateAsync(new MutationSample { Name = "cancelled" },
            cancellationToken: cancellationToken),
        SingleMutationOperation.Delete => executor.DeleteAsync(new MutationSample { Name = "cancelled" },
            cancellationToken: cancellationToken),
        SingleMutationOperation.Procedure => executor.ExecuteProcedureAsync("usp_cancelled",
            cancellationToken: cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    /// <summary>
    /// 单体 Mutation 操作类型。
    /// </summary>
    public enum SingleMutationOperation
    {
        /// <summary>插入。</summary>
        Insert,

        /// <summary>更新。</summary>
        Update,

        /// <summary>删除。</summary>
        Delete,

        /// <summary>存储过程。</summary>
        Procedure
    }

    /// <summary>
    /// 批量 Mutation 操作类型。
    /// </summary>
    public enum BatchMutationOperation
    {
        /// <summary>插入。</summary>
        Insert,

        /// <summary>更新。</summary>
        Update,

        /// <summary>删除。</summary>
        Delete
    }

    /// <summary>
    /// 一旦被枚举就失败的实体序列。
    /// </summary>
    /// <typeparam name="T">实体类型。</typeparam>
    private sealed class ThrowOnEnumerationEnumerable<T> : IEnumerable<T>
    {
        /// <summary>
        /// 枚举次数。
        /// </summary>
        public int EnumerationCount { get; private set; }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            throw new InvalidOperationException("实体序列不应被枚举。");
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 映射到测试表的实体。
    /// </summary>
    [Table("mutation_samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 实体名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 映射到单主键删除测试表的实体。
    /// </summary>
    [Table("delete_samples")]
    private sealed class DeleteSample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [System.ComponentModel.DataAnnotations.Key]
        public int Id { get; set; }
    }

    /// <summary>
    /// 映射到 Update 策略测试表的实体。
    /// </summary>
    [Table("update_samples")]
    private sealed class UpdateSample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [System.ComponentModel.DataAnnotations.Key]
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 映射到带并发令牌 Update 测试表的实体。
    /// </summary>
    [Table("concurrency_update_samples")]
    private sealed class ConcurrencyUpdateSample
    {
        /// <summary>主键。</summary>
        [System.ComponentModel.DataAnnotations.Key]
        public int Id { get; set; }

        /// <summary>名称。</summary>
        public string Name { get; set; }

        /// <summary>并发令牌。</summary>
        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        public string Version { get; set; }
    }

    /// <summary>
    /// 映射到带并发令牌 Delete 测试表的实体。
    /// </summary>
    [Table("concurrency_delete_samples")]
    private sealed class ConcurrencyDeleteSample
    {
        /// <summary>主键。</summary>
        [System.ComponentModel.DataAnnotations.Key]
        public int Id { get; set; }

        /// <summary>并发令牌。</summary>
        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        public string Version { get; set; }
    }

    /// <summary>
    /// 用于验证执行器批量行为的测试渲染器。
    /// </summary>
    private sealed class TestBatchUpdateRenderer : ISqlBatchUpdateRenderer
    {
        /// <inheritdoc />
        public string ProviderKey => "test.mutation-batch";

        /// <inheritdoc />
        public bool CanRender(SqlBatchUpdateRenderContext context) => true;

        /// <summary>最近一次上下文中的实体数量。</summary>
        public int LastEntityCount { get; private set; }

        /// <inheritdoc />
        public SqlMutationCommand Render(SqlBatchUpdateRenderContext context)
        {
            LastEntityCount = context.Entities.Count;
            return new SqlMutationCommand("Update [concurrency_update_samples] Set [Name] = @_p_0",
                new[] { new SqlParam("@_p_0", "updated") });
        }
    }

    /// <summary>
    /// 用于记录执行命令的 SQL Executor。
    /// </summary>
    private sealed class RecordingExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化记录型执行器。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="databaseType">测试 Provider 的数据库类型。</param>
        public RecordingExecutor(IServiceProvider serviceProvider, DatabaseType databaseType)
            : base(serviceProvider, CreateOptions(databaseType))
        {
        }

        /// <summary>
        /// 创建绑定测试 Provider Key 的执行器选项。
        /// </summary>
        /// <param name="databaseType">测试 Provider 的数据库类型。</param>
        /// <returns>执行器选项。</returns>
        private static SqlOptions CreateOptions(DatabaseType databaseType)
        {
            var options = new SqlOptions { DatabaseType = databaseType };
            options.SetDatabaseContext(new DatabaseContext
            {
                DataSource = new SqlDataSourceDescriptor
                {
                    DatabaseType = databaseType,
                    ProviderKey = "test.mutation-batch"
                }
            });
            return options;
        }

        /// <summary>
        /// 已执行命令的不可变快照集合。
        /// </summary>
        public List<RecordedCommand> Commands { get; } = new();

        /// <summary>
        /// 异步命令收到的取消令牌集合。
        /// </summary>
        public List<CancellationToken> CancellationTokens { get; } = new();

        /// <summary>
        /// 异步命令执行后触发的测试回调。
        /// </summary>
        public Action AfterExecuteAsync { get; set; }

        /// <summary>
        /// 同步命令执行时抛出的测试异常。
        /// </summary>
        public Exception ExecuteException { get; set; }

        /// <summary>
        /// 异步命令执行时返回的测试异常。
        /// </summary>
        public Exception ExecuteAsyncException { get; set; }

        /// <summary>
        /// 根据已记录命令返回模拟的实际影响行数。
        /// </summary>
        public Func<int> AffectedRows { get; set; }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;

        /// <inheritdoc />
        public override int ExecuteSql(string sql, object param = null, int? timeout = null)
        {
            if (ExecuteException != null)
                throw ExecuteException;
            var parameters = (param as IEnumerable<SqlParam>)?.ToArray() ?? Array.Empty<SqlParam>();
            var command = new RecordedCommand(sql, parameters);
            Commands.Add(command);
            return AffectedRows?.Invoke() ?? parameters.Length;
        }

        /// <inheritdoc />
        public override Task<int> ExecuteSqlAsync(string sql, object param = null, int? timeout = null,
            CancellationToken cancellationToken = default)
        {
            CancellationTokens.Add(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (ExecuteAsyncException != null)
                return Task.FromException<int>(ExecuteAsyncException);
            var parameters = (param as IEnumerable<SqlParam>)?.ToArray() ?? Array.Empty<SqlParam>();
            var command = new RecordedCommand(sql, parameters);
            Commands.Add(command);
            AfterExecuteAsync?.Invoke();
            return Task.FromResult(AffectedRows?.Invoke() ?? parameters.Length);
        }
    }

    /// <summary>
    /// 已记录的 SQL 命令快照。
    /// </summary>
    /// <param name="Sql">已执行命令的 SQL 文本。</param>
    /// <param name="Parameters">已执行命令的参数快照。</param>
    private sealed record RecordedCommand(string Sql, IReadOnlyList<SqlParam> Parameters);

    /// <summary>
    /// 声明批量 Insert 能力的测试 Provider。
    /// </summary>
    private sealed class TestProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 初始化测试 Provider。
        /// </summary>
        /// <param name="supportsMultiRowValues">是否支持标准多行 Values。</param>
        /// <param name="maxParameterCount">Provider 允许的最大参数数量。</param>
        public TestProvider(bool supportsMultiRowValues, int? maxParameterCount)
        {
            Profile = new SqlProviderProfile
            {
                Mutation = new SqlProviderMutationCapabilities { SupportsMultiRowValues = supportsMultiRowValues },
                Execution = new SqlProviderExecutionCapabilities { SupportsCancellation = true },
                Limits = new SqlProviderLimits { MaxParameterCount = maxParameterCount }
            };
        }

        /// <inheritdoc />
        public string Key => "test.mutation-batch";

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.Sqlite;

        /// <inheritdoc />
        public IDialect Dialect { get; } = new TestDialect();

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new TestPaginationRenderer();

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver { get; } = new ParamLiteralsResolver();

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; }

    }

    /// <summary>
    /// 使用方括号标识符和 <c>@_p_n</c> 参数名的测试方言。
    /// </summary>
    private sealed class TestDialect : DialectBase
    {
    }

    /// <summary>
    /// 测试 Provider 的分页渲染器。
    /// </summary>
    private sealed class TestPaginationRenderer : ISqlPaginationRenderer
    {
        /// <inheritdoc />
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Limit {limitParameterName} Offset {offsetParameterName}";
    }
}