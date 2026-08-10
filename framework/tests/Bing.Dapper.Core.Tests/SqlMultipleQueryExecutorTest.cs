using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Multiple;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Data;
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
        using var provider = CreateServiceProvider();
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
        using var provider = CreateServiceProvider();
        using var executor = new UnsupportedMultipleQueryExecutor(provider);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ExecuteAsync(null));

        // Assert
        Assert.Equal("command", exception.ParamName);
    }

    /// <summary>
    /// 测试目的：Provider 不支持多结果集时，同步入口必须在打开连接或创建命令前拒绝有效命令。
    /// </summary>
    [Fact]
    public void Execute_WhenMultipleResultsAreUnsupported_ShouldRejectBeforeConnectionAccess()
    {
        // Arrange
        using var provider = CreateServiceProvider();
        var connection = new Mock<IDbConnection>();
        using var executor = new UnsupportedMultipleQueryExecutor(provider, connection.Object);
        var command = new SqlMultipleQueryCommand("Select 1; Select 2", Array.Empty<SqlParam>());

        // Act
        Assert.Throws<NotSupportedException>(() => executor.Execute(command));

        // Assert
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：Provider 不支持多结果集时，异步入口必须在打开连接或创建命令前拒绝有效命令。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenMultipleResultsAreUnsupported_ShouldRejectBeforeConnectionAccess()
    {
        // Arrange
        using var provider = CreateServiceProvider();
        var connection = new Mock<IDbConnection>();
        using var executor = new UnsupportedMultipleQueryExecutor(provider, connection.Object);
        var command = new SqlMultipleQueryCommand("Select 1; Select 2", Array.Empty<SqlParam>());

        // Act
        await Assert.ThrowsAsync<NotSupportedException>(() => executor.ExecuteAsync(command));

        // Assert
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
    }

    /// <summary>
    /// 测试目的：预取消的多结果集异步命令应在执行 Hook 前取消，不能被跳过 Hook 吞掉。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeExecution_ShouldThrowBeforeExecuteBefore()
    {
        // Arrange
        using var provider = CreateServiceProvider(supportsMultipleResultSets: true);
        var connection = new Mock<IDbConnection>();
        using var executor = new SkippingMultipleQueryExecutor(provider, connection.Object);
        var command = new SqlMultipleQueryCommand("Select 1; Select 2", Array.Empty<SqlParam>());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(command,
            cancellationToken: cancellationTokenSource.Token));

        // Assert
        Assert.Equal(0, executor.ExecuteBeforeCount);
        connection.Verify(item => item.Open(), Times.Never);
        connection.Verify(item => item.CreateCommand(), Times.Never);
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
        public UnsupportedMultipleQueryExecutor(IServiceProvider serviceProvider, IDbConnection connection = null)
            : base(serviceProvider, new SqlOptions { Connection = connection })
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 记录执行前 Hook 的多结果集测试执行器。
    /// </summary>
    private sealed class SkippingMultipleQueryExecutor : SqlMultipleQueryExecutorBase
    {
        /// <summary>
        /// 初始化测试执行器。
        /// </summary>
        public SkippingMultipleQueryExecutor(IServiceProvider serviceProvider, IDbConnection connection)
            : base(serviceProvider, CreateOptions(connection))
        {
        }

        /// <summary>
        /// 执行前 Hook 调用次数。
        /// </summary>
        public int ExecuteBeforeCount { get; private set; }

        /// <inheritdoc />
        protected override bool ExecuteBefore()
        {
            ExecuteBeforeCount++;
            return false;
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 创建固定到多结果集测试 Provider 的执行选项。
    /// </summary>
    /// <param name="connection">测试连接。</param>
    private static SqlOptions CreateOptions(IDbConnection connection)
    {
        var options = new SqlOptions { Connection = connection, DatabaseType = DatabaseType.SqlServer };
        options.SetDatabaseContext(new DatabaseContext
        {
            DbKey = "multiple-profile-gate",
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "multiple-profile-gate",
                DatabaseType = DatabaseType.SqlServer,
                ProviderKey = UnsupportedMultipleResultProvider.ProviderKey
            }
        });
        return options;
    }

    /// <summary>
    /// 创建注册仅支持单结果集 Provider 的测试服务容器。
    /// </summary>
    private static ServiceProvider CreateServiceProvider(bool supportsMultipleResultSets = false)
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSingleton<ISqlProvider>(new UnsupportedMultipleResultProvider(supportsMultipleResultSets));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 仅用于验证统一 Profile 多结果集 Gate 的 Provider。
    /// </summary>
    private sealed class UnsupportedMultipleResultProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>测试 Provider Key。</summary>
        public const string ProviderKey = "test.multiple.unsupported";

        /// <summary>
        /// 初始化测试 Provider。
        /// </summary>
        /// <param name="supportsMultipleResultSets">是否支持多结果集。</param>
        public UnsupportedMultipleResultProvider(bool supportsMultipleResultSets) => Profile = new SqlProviderProfile
        {
            Execution = new SqlProviderExecutionCapabilities
            {
                SupportsMultipleResultSets = supportsMultipleResultSets
            }
        };

        /// <inheritdoc />
        public string Key => ProviderKey;

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect { get; } = new Mock<IDialect>().Object;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new Mock<ISqlClauseFactory>().Object;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser { get; } = new Mock<ISqlTableReferenceParser>().Object;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new Mock<ISqlPaginationRenderer>().Object;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory { get; } = new Mock<IParameterManagerFactory>().Object;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver { get; } = new Mock<IParamLiteralsResolver>().Object;

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; }
    }
}