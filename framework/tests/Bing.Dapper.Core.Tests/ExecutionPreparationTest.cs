using System.Data;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Mutations;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// SQL 执行准备的延迟工作测试。
/// </summary>
public class ExecutionPreparationTest
{
    /// <summary>
    /// 测试目的：同步 Direct Builder 执行在 Trace 和诊断均关闭时不得生成调试 SQL 或参数元数据。
    /// </summary>
    [Fact]
    public void ExecuteMutation_WhenTraceAndDiagnosticsAreDisabled_ShouldNotRenderDebugSqlOrCreateDiagnostics()
    {
        // Arrange
        var builder = CreateBuilder();
        var executor = CreateExecutor();

        // Act
        var result = executor.ExecuteMutation(builder.Object.ToSqlWriteCommand());

        // Assert
        Assert.Equal(1, result);
        builder.Verify(item => item.ToDebugSql(It.IsAny<string>()), Times.Never);
        Assert.Equal(0, executor.BuilderDiagnosticRequests);
    }

    /// <summary>
    /// 测试目的：异步 Direct Builder 执行在 Trace 和诊断均关闭时不得生成调试 SQL 或参数元数据。
    /// </summary>
    [Fact]
    public async Task ExecuteMutationAsync_WhenTraceAndDiagnosticsAreDisabled_ShouldNotRenderDebugSqlOrCreateDiagnostics()
    {
        // Arrange
        var builder = CreateBuilder();
        var executor = CreateExecutor();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteMutationAsync(builder.Object.ToSqlWriteCommand()));

        // Assert
        Assert.Contains("Async operations require use of a DbConnection", exception.Message);
        builder.Verify(item => item.ToDebugSql(It.IsAny<string>()), Times.Never);
        Assert.Equal(0, executor.BuilderDiagnosticRequests);
    }

    /// <summary>
    /// 测试目的：普通同步写入命令的 Provider 与 Executor 不一致时，必须在创建命令前拒绝。
    /// </summary>
    [Fact]
    public void ExecuteMutation_WhenWriteCommandProviderMismatches_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var builder = CreateBuilder("bing.sqlite");
        var executor = CreateExecutor();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => executor.ExecuteMutation(builder.Object.ToSqlWriteCommand()));

        // Assert
        Assert.Equal("写入命令 Provider bing.sqlite 与当前 Executor Provider bing.sqlserver 不一致，不能执行。",
            exception.Message);
    }

    /// <summary>
    /// 测试目的：普通异步写入命令的 Provider 与 Executor 不一致时，必须在异步事务或命令创建前拒绝。
    /// </summary>
    [Fact]
    public async Task ExecuteMutationAsync_WhenWriteCommandProviderMismatches_ShouldRejectBeforeCommandCreation()
    {
        // Arrange
        var builder = CreateBuilder("bing.sqlite");
        var executor = CreateExecutor();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteMutationAsync(builder.Object.ToSqlWriteCommand()));

        // Assert
        Assert.Equal("写入命令 Provider bing.sqlite 与当前 Executor Provider bing.sqlserver 不一致，不能执行。",
            exception.Message);
    }

    /// <summary>
    /// 创建用于 Direct Builder 执行的测试 Builder。
    /// </summary>
    private static Mock<ISqlBuilder> CreateBuilder(string providerKey = "bing.sqlserver")
    {
        var builder = new Mock<ISqlBuilder>();
        var provider = new Mock<ISqlProvider>();
        provider.SetupGet(item => item.Key).Returns(providerKey);
        builder.SetupGet(item => item.Provider).Returns(provider.Object);
        builder.SetupGet(item => item.OperationKind).Returns(SqlOperationKind.Update);
        builder.Setup(item => item.Clone()).Returns(builder.Object);
        builder.Setup(item => item.ToSql()).Returns("Update samples Set Name = 'updated'");
        builder.Setup(item => item.ToDebugSql(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Trace 关闭时不得生成调试 SQL。"));
        return builder;
    }

    /// <summary>
    /// 创建具有外部连接和空参数绑定器的测试执行器。
    /// </summary>
    private static PreparationTestExecutor CreateExecutor()
    {
        var command = new Mock<IDbCommand>();
        command.SetupGet(item => item.Parameters).Returns(new Mock<IDataParameterCollection>().Object);
        command.Setup(item => item.ExecuteNonQuery()).Returns(1);
        var connection = new Mock<IDbConnection>();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        connection.Setup(item => item.CreateCommand()).Returns(command.Object);
        return new PreparationTestExecutor(CreateServiceProvider(), new SqlOptions { Connection = connection.Object });
    }

    /// <summary>
    /// 创建测试服务提供程序。
    /// </summary>
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        var provider = new Mock<ISqlProvider>();
        provider.SetupGet(item => item.Key).Returns("bing.sqlserver");
        services.AddSingleton(provider.Object);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 记录诊断元数据请求次数的执行器测试替身。
    /// </summary>
    private sealed class PreparationTestExecutor : SqlExecutorBase
    {
        /// <summary>
        /// 初始化执行器测试替身。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public PreparationTestExecutor(IServiceProvider serviceProvider, SqlOptions options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// Builder 参数诊断元数据请求次数。
        /// </summary>
        public int BuilderDiagnosticRequests { get; private set; }

        /// <inheritdoc />
        protected override IReadOnlyCollection<SqlParameterDiagnosticInfo> GetSqlParameterDiagnostics(
            ISqlBuilder builder, string sql)
        {
            BuilderDiagnosticRequests++;
            return Array.Empty<SqlParameterDiagnosticInfo>();
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

}