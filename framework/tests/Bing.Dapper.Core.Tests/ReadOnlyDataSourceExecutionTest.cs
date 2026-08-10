using System.Data;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Mutations;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 只读 SQL 数据源的执行边界测试。
/// </summary>
public class ReadOnlyDataSourceExecutionTest
{
    /// <summary>
    /// 测试目的：只读数据源上的同步结构化 Mutation 应在描述已冻结后、创建命令前失败。
    /// </summary>
    [Fact]
    public void Execute_WhenDataSourceIsReadOnly_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var connection = CreateConnection();
        var executor = CreateExecutor(connection.Object);
        var builder = CreateMutationBuilder();

        // Act
        var description = builder.Object.ToMutationDescription();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.Execute(description));

        // Assert
        Assert.Contains("reporting", exception.Message);
        builder.Verify(item => item.ToSql(), Times.Once);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        connection.Verify(item => item.Open(), Times.Never);
    }

    /// <summary>
    /// 测试目的：只读数据源上的异步结构化 Mutation 应在描述已冻结后、创建命令前失败。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenDataSourceIsReadOnly_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var connection = CreateConnection();
        var executor = CreateExecutor(connection.Object);
        var builder = CreateMutationBuilder();

        // Act
        var description = builder.Object.ToMutationDescription();

        // Act
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => executor.ExecuteAsync(description));

        // Assert
        Assert.Contains("reporting", exception.Message);
        builder.Verify(item => item.ToSql(), Times.Once);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        connection.Verify(item => item.Open(), Times.Never);
    }

    /// <summary>
    /// 测试目的：只读数据源上的执行型存储过程应在创建命令前失败。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenDataSourceIsReadOnly_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var connection = CreateConnection();
        var executor = CreateExecutor(connection.Object);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => executor.ExecuteProcedure("UpdateReport"));

        // Assert
        Assert.Contains("reporting", exception.Message);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        connection.Verify(item => item.Open(), Times.Never);
    }

    /// <summary>
    /// 测试目的：只读数据源上的查询型存储过程描述应在读取 Provider 能力和创建连接前拒绝。
    /// </summary>
    [Fact]
    public void Procedure_WhenDataSourceIsReadOnly_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var connection = CreateConnection();
        using var query = new ReadOnlyTestQuery(CreateServiceProvider(), CreateOptions(connection.Object));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => query.Procedure<int>("ReadReport"));

        // Assert
        Assert.Contains("reporting", exception.Message);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        connection.Verify(item => item.Open(), Times.Never);
    }

    /// <summary>
    /// 测试目的：原生 SQL 是调用方显式选择的执行边界，不应由框架进行关键字猜测拦截。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenDataSourceIsReadOnly_ShouldPreserveExplicitRawSqlExecutionBoundary()
    {
        // Arrange
        var command = new Mock<IDbCommand>();
        command.SetupGet(item => item.Parameters).Returns(new Mock<IDataParameterCollection>().Object);
        command.Setup(item => item.ExecuteNonQuery()).Returns(1);
        var connection = CreateConnection();
        connection.Setup(item => item.CreateCommand()).Returns(command.Object);
        var executor = CreateExecutor(connection.Object);

        // Act
        var result = executor.ExecuteSql("Update report Set Status = 'archived'");

        // Assert
        Assert.Equal(1, result);
        connection.Verify(item => item.CreateCommand(), Times.Once);
    }

    /// <summary>
    /// 测试目的：只读数据源上的同步批量 Mutation 应在枚举实体和生成批次 SQL 前失败。
    /// </summary>
    /// <param name="operation">待执行的批量 Mutation 类型。</param>
    [Theory]
    [InlineData(BatchMutationOperation.Insert)]
    [InlineData(BatchMutationOperation.Update)]
    [InlineData(BatchMutationOperation.Delete)]
    public void ExecuteBatch_WhenDataSourceIsReadOnly_ShouldRejectBeforeEnumeratingEntities(
        BatchMutationOperation operation)
    {
        // Arrange
        var connection = CreateConnection();
        var executor = CreateExecutor(connection.Object);
        var entities = new ThrowingEnumerable<ReadOnlyEntity>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => ExecuteBatch(executor, operation, entities));

        // Assert
        Assert.Contains("reporting", exception.Message);
        Assert.Equal(0, entities.EnumerationCount);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        connection.Verify(item => item.Open(), Times.Never);
    }

    /// <summary>
    /// 测试目的：只读数据源上的异步批量 Mutation 应在枚举实体和生成批次 SQL 前失败。
    /// </summary>
    /// <param name="operation">待执行的批量 Mutation 类型。</param>
    [Theory]
    [InlineData(BatchMutationOperation.Insert)]
    [InlineData(BatchMutationOperation.Update)]
    [InlineData(BatchMutationOperation.Delete)]
    public async Task ExecuteBatchAsync_WhenDataSourceIsReadOnly_ShouldRejectBeforeEnumeratingEntities(
        BatchMutationOperation operation)
    {
        // Arrange
        var connection = CreateConnection();
        var executor = CreateExecutor(connection.Object);
        var entities = new ThrowingEnumerable<ReadOnlyEntity>();

        // Act
        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await ExecuteBatchAsync(executor, operation, entities));

        // Assert
        Assert.Contains("reporting", exception.Message);
        Assert.Equal(0, entities.EnumerationCount);
        connection.Verify(item => item.CreateCommand(), Times.Never);
        connection.Verify(item => item.Open(), Times.Never);
    }

    /// <summary>
    /// 测试目的：只读数据源上的事务作用域应在打开连接和开始事务前失败。
    /// </summary>
    [Fact]
    public void BeginTransactionScope_WhenDataSourceIsReadOnly_ShouldRejectBeforeConnectionOpen()
    {
        // Arrange
        var connection = CreateConnection();
        var query = new ReadOnlyTestQuery(CreateServiceProvider(), CreateOptions(connection.Object));
        var queryFactory = new Mock<ISqlQueryFactory>();
        queryFactory.Setup(item => item.Create<ISqlQuery>()).Returns(query);
        var executorFactory = new Mock<ISqlExecutorFactory>();
        var factory = new SqlTransactionScopeFactory(queryFactory.Object, executorFactory.Object);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => factory.Begin());

        // Assert
        Assert.Contains("reporting", exception.Message);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.BeginTransaction(It.IsAny<IsolationLevel>()), Times.Never);
    }

    /// <summary>
    /// 创建只读数据源使用的测试连接。
    /// </summary>
    private static Mock<IDbConnection> CreateConnection()
    {
        var connection = new Mock<IDbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Closed);
        return connection;
    }

    /// <summary>
    /// 创建只读数据源执行器。
    /// </summary>
    /// <param name="connection">外部测试连接。</param>
    private static ReadOnlyTestExecutor CreateExecutor(IDbConnection connection) =>
        new(CreateServiceProvider(), CreateOptions(connection));

    /// <summary>
    /// 创建只读数据源配置。
    /// </summary>
    /// <param name="connection">外部测试连接。</param>
    private static SqlOptions CreateOptions(IDbConnection connection)
    {
        var options = new SqlOptions { Connection = connection };
        options.SetDatabaseContext(new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "reporting",
                IsReadOnly = true
            }
        });
        return options;
    }

    /// <summary>
    /// 创建测试服务提供程序。
    /// </summary>
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建结构化 Mutation Builder。
    /// </summary>
    private static Mock<ISqlBuilder> CreateMutationBuilder()
    {
        var builder = new Mock<ISqlBuilder>();
        var provider = new Mock<ISqlProvider>();
        provider.SetupGet(item => item.Key).Returns("test.read-only-builder");
        builder.SetupGet(item => item.Provider).Returns(provider.Object);
        builder.SetupGet(item => item.OperationKind).Returns(SqlOperationKind.Update);
        builder.Setup(item => item.ToSql()).Returns("Update samples Set Name = 'updated'");
        return builder;
    }

    /// <summary>
    /// 执行指定的同步批量 Mutation。
    /// </summary>
    /// <param name="executor">当前测试执行器。</param>
    /// <param name="operation">待执行的批量 Mutation 类型。</param>
    /// <param name="entities">待写入的实体序列。</param>
    private static int ExecuteBatch(ReadOnlyTestExecutor executor, BatchMutationOperation operation,
        IEnumerable<ReadOnlyEntity> entities) => operation switch
    {
        BatchMutationOperation.Insert => executor.InsertBatch(entities),
        BatchMutationOperation.Update => executor.UpdateBatch(entities),
        BatchMutationOperation.Delete => executor.DeleteBatch(entities),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    /// <summary>
    /// 执行指定的异步批量 Mutation。
    /// </summary>
    /// <param name="executor">当前测试执行器。</param>
    /// <param name="operation">待执行的批量 Mutation 类型。</param>
    /// <param name="entities">待写入的实体序列。</param>
    private static Task<int> ExecuteBatchAsync(ReadOnlyTestExecutor executor, BatchMutationOperation operation,
        IEnumerable<ReadOnlyEntity> entities) => operation switch
    {
        BatchMutationOperation.Insert => executor.InsertBatchAsync(entities),
        BatchMutationOperation.Update => executor.UpdateBatchAsync(entities),
        BatchMutationOperation.Delete => executor.DeleteBatchAsync(entities),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    /// <summary>
    /// 只读数据源执行器测试替身。
    /// </summary>
    private sealed class ReadOnlyTestExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化只读数据源执行器测试替身。
        /// </summary>
        /// <param name="serviceProvider">测试服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public ReadOnlyTestExecutor(IServiceProvider serviceProvider, SqlOptions options)
            : base(serviceProvider, options)
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 批量 Mutation 操作类型。
    /// </summary>
    public enum BatchMutationOperation
    {
        /// <summary>
        /// 插入实体。
        /// </summary>
        Insert,

        /// <summary>
        /// 更新实体。
        /// </summary>
        Update,

        /// <summary>
        /// 删除实体。
        /// </summary>
        Delete
    }

    /// <summary>
    /// 只读边界测试使用的实体。
    /// </summary>
    private sealed class ReadOnlyEntity
    {
    }

    /// <summary>
    /// 枚举时抛出异常的测试序列。
    /// </summary>
    /// <typeparam name="T">序列元素类型。</typeparam>
    private sealed class ThrowingEnumerable<T> : IEnumerable<T>
    {
        /// <summary>
        /// 已发起的枚举次数。
        /// </summary>
        public int EnumerationCount { get; private set; }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            throw new InvalidOperationException("只读数据源拒绝前不得枚举实体。");
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 只读数据源查询测试替身。
    /// </summary>
    private sealed class ReadOnlyTestQuery : SqlQueryBase
    {
        /// <summary>
        /// 初始化只读数据源查询测试替身。
        /// </summary>
        /// <param name="serviceProvider">测试服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public ReadOnlyTestQuery(IServiceProvider serviceProvider, SqlOptions options)
            : base(serviceProvider, options)
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }
}