using System.Data;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Reflection;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// SQL Query 与 Executor 实例执行租约测试。
/// </summary>
public class ExecutionLeaseTest
{
    /// <summary>
    /// 测试目的：同一 Query 的执行范围内重入其他公共执行入口时应立即失败，操作结束后实例可复用。
    /// </summary>
    [Fact]
    public void Query_WhenExecutionIsActive_ShouldRejectReentrantOperationAndAllowReuse()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var query = new ReentrantQuery(provider) { VerifyReentrantOperation = true };

        // Act
        var result = query.Procedure<object>("sample").Scalar();

        // Assert
        Assert.Null(result);
        Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
            query.ReentrantException.Message);
        Assert.Null(query.Procedure<object>("sample").Scalar());
    }

    /// <summary>
    /// 测试目的：Query 执行前发生异常时必须归还租约，避免后续操作永久阻塞。
    /// </summary>
    [Fact]
    public void Query_WhenExecutionFails_ShouldReleaseLease()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var query = new ReentrantQuery(provider) { ThrowOnExecuteBefore = true };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.Procedure<object>("sample").Scalar());

        // Assert
        Assert.Equal("受控执行前异常。", exception.Message);
        Assert.Null(query.Procedure<object>("sample").Scalar());
    }

    /// <summary>
    /// 测试目的：Root Query 已释放时，创建存储过程描述必须立即失败，避免延迟到终结方法才暴露无效状态。
    /// </summary>
    [Fact]
    public void Procedure_WhenQueryDisposed_ShouldRejectDescriptionCreation()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var query = new ReentrantQuery(provider);
        query.Dispose();

        // Act and Assert
        Assert.Throws<ObjectDisposedException>(() => query.Procedure<object>("sample"));
    }

    /// <summary>
    /// 测试目的：异步 Query 执行范围内重入其他公共执行入口时应立即失败，操作结束后实例可复用。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenExecutionIsActive_ShouldRejectReentrantOperationAndAllowReuse()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var query = new ReentrantQuery(provider) { VerifyReentrantOperation = true };

        // Act
        var result = await query.Procedure<object>("sample").ScalarAsync();

        // Assert
        Assert.Null(result);
        Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
            query.ReentrantException.Message);
        Assert.Null(await query.Procedure<object>("sample").ScalarAsync());
    }

    /// <summary>
    /// 测试目的：同一 Executor 的执行范围内重入其他命令入口时应立即失败，操作结束后实例可复用。
    /// </summary>
    [Fact]
    public void Executor_WhenExecutionIsActive_ShouldRejectReentrantOperationAndAllowReuse()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var executor = new ReentrantExecutor(provider) { VerifyReentrantOperation = true };

        // Act
        var result = executor.ExecuteSql("Update samples Set Name='first'");

        // Assert
        Assert.Equal(0, result);
        Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
            executor.ReentrantException.Message);
        Assert.Equal(0, executor.ExecuteSql("Update samples Set Name='second'"));
    }

    /// <summary>
    /// 测试目的：异步 Executor 执行范围内重入其他命令入口时应立即失败，操作结束后实例可复用。
    /// </summary>
    [Fact]
    public async Task ExecutorAsync_WhenExecutionIsActive_ShouldRejectReentrantOperationAndAllowReuse()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var executor = new ReentrantExecutor(provider) { VerifyReentrantOperation = true };

        // Act
        var result = await executor.ExecuteSqlAsync("Update samples Set Name='first'");

        // Assert
        Assert.Equal(0, result);
        Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
            executor.ReentrantException.Message);
        Assert.Equal(0, await executor.ExecuteSqlAsync("Update samples Set Name='second'"));
    }

    /// <summary>
    /// 测试目的：自有事务释放失败时仍必须释放自有连接，避免 Dispose 因首个异常造成连接泄漏。
    /// </summary>
    [Fact]
    public void Dispose_WhenOwnedTransactionDisposeFails_ShouldStillDisposeOwnedConnection()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var query = new ReentrantQuery(provider);
        var transactionException = new InvalidOperationException("受控事务释放异常。");
        var transaction = new Mock<IDbTransaction>();
        var connection = new Mock<IDbConnection>();
        transaction.Setup(item => item.Dispose()).Throws(transactionException);
        SetPrivateField(query, "_transaction", transaction.Object);
        SetPrivateField(query, "_transactionOwnership", SqlResourceOwnership.Owned);
        SetPrivateField(query, "_connection", connection.Object);
        SetPrivateField(query, "_connectionOwnership", SqlResourceOwnership.Owned);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(query.Dispose);

        // Assert
        Assert.Same(transactionException, exception);
        transaction.Verify(item => item.Dispose(), Times.Once);
        connection.Verify(item => item.Dispose(), Times.Once);
    }

    /// <summary>
    /// 为资源释放边界测试设置 Query 基类的私有状态。
    /// </summary>
    /// <param name="target">待设置状态的 Query 实例。</param>
    /// <param name="fieldName">私有字段名称。</param>
    /// <param name="value">要写入字段的值。</param>
    private static void SetPrivateField(SqlQueryBase target, string fieldName, object value)
    {
        var field = typeof(SqlQueryBase).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    /// <summary>
    /// 使用执行前钩子重入另一执行入口的 Query 测试实现。
    /// </summary>
    private sealed class ReentrantQuery : SqlQueryBase
    {
        /// <summary>
        /// 初始化测试 Query。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        public ReentrantQuery(IServiceProvider serviceProvider) : base(serviceProvider, new SqlOptions())
        {
        }

        /// <summary>
        /// 是否验证重入行为。
        /// </summary>
        public bool VerifyReentrantOperation { get; set; }

        /// <summary>
        /// 是否在执行前抛出受控异常。
        /// </summary>
        public bool ThrowOnExecuteBefore { get; set; }

        /// <summary>
        /// 重入执行抛出的异常。
        /// </summary>
        public InvalidOperationException ReentrantException { get; private set; }

        /// <inheritdoc />
        protected override bool ExecuteBefore()
        {
            if (ThrowOnExecuteBefore)
            {
                ThrowOnExecuteBefore = false;
                throw new InvalidOperationException("受控执行前异常。");
            }
            if (VerifyReentrantOperation)
            {
                VerifyReentrantOperation = false;
                ReentrantException = Assert.Throws<InvalidOperationException>(() => Procedure<object>("sample").Scalar());
            }
            return false;
        }

        /// <inheritdoc />
        protected override void ExecuteAfter(object result)
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 使用执行前钩子重入另一命令入口的 Executor 测试实现。
    /// </summary>
    private sealed class ReentrantExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化测试 Executor。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        public ReentrantExecutor(IServiceProvider serviceProvider) : base(serviceProvider, new SqlOptions())
        {
        }

        /// <summary>
        /// 是否验证重入行为。
        /// </summary>
        public bool VerifyReentrantOperation { get; set; }

        /// <summary>
        /// 重入执行抛出的异常。
        /// </summary>
        public InvalidOperationException ReentrantException { get; private set; }

        /// <inheritdoc />
        protected override bool ExecuteBefore()
        {
            if (VerifyReentrantOperation)
            {
                VerifyReentrantOperation = false;
                ReentrantException = Assert.Throws<InvalidOperationException>(() => ExecuteProcedure("sample"));
            }
            return false;
        }

        /// <inheritdoc />
        protected override void ExecuteAfter(object result)
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }
}
