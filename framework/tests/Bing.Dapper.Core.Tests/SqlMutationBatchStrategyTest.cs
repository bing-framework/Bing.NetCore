using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
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
    /// 测试目的：异步单实体 Insert 在调用前已取消时，应将令牌传递到执行边界且不执行命令。
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
        Assert.Equal(cancellationTokenSource.Token, Assert.Single(executor.CancellationTokens));
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
    /// <returns>用于执行器测试的服务提供程序。</returns>
    private static ServiceProvider CreateServiceProvider(bool supportsMultiRowValues, int? maxParameterCount = null,
        ISqlTransactionScopeFactory transactionScopeFactory = null)
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSingleton<ISqlProvider>(new TestProvider(supportsMultiRowValues, maxParameterCount));
        if (transactionScopeFactory != null)
            services.AddSingleton(transactionScopeFactory);
        return services.BuildServiceProvider();
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
            : base(serviceProvider, new SqlOptions { DatabaseType = databaseType })
        {
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

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;

        /// <inheritdoc />
        public override int ExecuteSql(string sql, object param = null, int? timeout = null)
        {
            if (ExecuteException != null)
                throw ExecuteException;
            var parameters = (param as IEnumerable<SqlParam>)?.ToArray() ?? Array.Empty<SqlParam>();
            Commands.Add(new RecordedCommand(sql, parameters));
            return parameters.Length;
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
            Commands.Add(new RecordedCommand(sql, parameters));
            AfterExecuteAsync?.Invoke();
            return Task.FromResult(parameters.Length);
        }
    }

    /// <summary>
    /// 已记录的 SQL 命令快照。
    /// </summary>
    /// <param name="sql">SQL 文本。</param>
    /// <param name="parameters">参数快照。</param>
    private sealed record RecordedCommand(string Sql, IReadOnlyList<SqlParam> Parameters);

    /// <summary>
    /// 声明批量 Insert 能力的测试 Provider。
    /// </summary>
    private sealed class TestProvider : ISqlProvider, ISqlProviderCapabilityProvider, ISqlParameterLimitProvider
    {
        /// <summary>
        /// 初始化测试 Provider。
        /// </summary>
        /// <param name="supportsMultiRowValues">是否支持标准多行 Values。</param>
        /// <param name="maxParameterCount">Provider 允许的最大参数数量。</param>
        public TestProvider(bool supportsMultiRowValues, int? maxParameterCount)
        {
            Capabilities = new SqlProviderCapabilities(supportsMultiRowValues: supportsMultiRowValues);
            MaxParameterCount = maxParameterCount;
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
        public SqlProviderCapabilities Capabilities { get; }

        /// <inheritdoc />
        public int? MaxParameterCount { get; }
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