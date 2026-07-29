using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 多结果集查询执行器输入校验测试。
/// </summary>
public class SqlMultipleQueryExecutorTest
{
    /// <summary>
    /// 测试目的：Provider 未声明多结果集能力时，同步入口仍应优先拒绝空命令参数。
    /// </summary>
    [Fact]
    public void Execute_WhenCommandIsNull_ShouldThrowArgumentNullBeforeCapabilityCheck()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var executor = new UnsupportedMultipleQueryExecutor(provider);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => executor.Execute(null));

        // Assert
        Assert.Equal("command", exception.ParamName);
    }

    /// <summary>
    /// 测试目的：Provider 未声明多结果集能力时，异步入口仍应优先拒绝空命令参数。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCommandIsNull_ShouldThrowArgumentNullBeforeCapabilityCheck()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var executor = new UnsupportedMultipleQueryExecutor(provider);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ExecuteAsync(null));

        // Assert
        Assert.Equal("command", exception.ParamName);
    }

    /// <summary>
    /// 未声明多结果集能力的测试执行器。
    /// </summary>
    private sealed class UnsupportedMultipleQueryExecutor : SqlMultipleQueryExecutorBase
    {
        /// <summary>
        /// 初始化一个<see cref="UnsupportedMultipleQueryExecutor"/>类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        public UnsupportedMultipleQueryExecutor(IServiceProvider serviceProvider)
            : base(serviceProvider, new SqlOptions(), new SqlProviderCapabilities())
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }
}