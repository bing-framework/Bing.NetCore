using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Microsoft.Extensions.DependencyInjection;
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
        var result = query.ExecuteScalar();

        // Assert
        Assert.Null(result);
        Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
            query.ReentrantException.Message);
        Assert.Null(query.ExecuteScalar());
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
        var exception = Assert.Throws<InvalidOperationException>(() => query.ExecuteScalar());

        // Assert
        Assert.Equal("受控执行前异常。", exception.Message);
        Assert.Null(query.ExecuteScalar());
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
        var result = await query.ExecuteScalarAsync();

        // Assert
        Assert.Null(result);
        Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。",
            query.ReentrantException.Message);
        Assert.Null(await query.ExecuteScalarAsync());
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
                ReentrantException = Assert.Throws<InvalidOperationException>(() => ExecuteProcedureScalar("sample"));
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
