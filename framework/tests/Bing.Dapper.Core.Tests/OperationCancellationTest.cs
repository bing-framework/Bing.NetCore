using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Multiple;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 验证 Dapper 异步入口遵循当前业务操作的取消边界。
/// </summary>
public sealed class OperationCancellationTest
{
    /// <summary>
    /// 测试目的：ambient operation 已取消时，在访问数据库前标记 root rollback-only。
    /// </summary>
    [Fact]
    public async Task MultipleQuery_WhenAmbientOperationIsCancelled_ShouldRejectBeforeConnectionAccess()
    {
        var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddBingOperations();
        services.AddSingleton<ISqlProvider>(new OperationCancellationProvider());
        services.Replace(ServiceDescriptor.Singleton<ISqlDbConnectionFactoryResolver>(resolver.Object));
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var root = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
            .BeginOperation(cancellationToken: cancellation.Token);
        await using (root)
        using (var executor = new OperationCancellationExecutor(scope.ServiceProvider))
        {
            var command = new SqlMultipleQueryCommand("Select 1; Select 2", Array.Empty<SqlParam>());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(command));

            Assert.True(root.IsRollbackOnly);
            resolver.Verify(item => item.Create(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    /// <summary>
    /// 验证参与者取消后在访问连接前拒绝多结果查询。
    /// </summary>
    [Fact]
    public async Task MultipleQuery_WhenParticipantIsCancelled_ShouldRejectBeforeConnectionAccess()
    {
        var resolver = new Mock<ISqlDbConnectionFactoryResolver>();
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddBingOperations();
        services.AddSingleton<ISqlProvider>(new OperationCancellationProvider());
        services.Replace(ServiceDescriptor.Singleton<ISqlDbConnectionFactoryResolver>(resolver.Object));
        using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var root = manager.BeginOperation();
        var participant = manager.BeginOperation(cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await using (root)
        await using (participant)
        using (var executor = new OperationCancellationExecutor(scope.ServiceProvider))
        {
            var command = new SqlMultipleQueryCommand("Select 1; Select 2", Array.Empty<SqlParam>());

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => executor.ExecuteAsync(command, cancellationToken: CancellationToken.None));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.True(root.IsRollbackOnly);
            Assert.Same(root, manager.Current);
            resolver.Verify(item => item.Create(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    /// <summary>
    /// 为取消路径提供支持多结果集和协作取消声明的 SQL Server 风格 Provider。
    /// </summary>
    private sealed class OperationCancellationProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <inheritdoc />
        public string Key => "test.operation-cancellation";

        /// <inheritdoc />
        public DatabaseType DatabaseType => Bing.Data.Enums.DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect { get; } = new Mock<IDialect>().Object;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory { get; } = new Mock<ISqlClauseFactory>().Object;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser { get; } =
            new Mock<ISqlTableReferenceParser>().Object;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer { get; } = new Mock<ISqlPaginationRenderer>().Object;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory { get; } =
            new Mock<IParameterManagerFactory>().Object;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver { get; } = new Mock<IParamLiteralsResolver>().Object;

        /// <inheritdoc />
        public SqlProviderProfile Profile { get; } = new()
        {
            Execution = new SqlProviderExecutionCapabilities
            {
                SupportsMultipleResultSets = true,
                SupportsCancellation = true
            }
        };
    }

    /// <summary>
    /// 供 ambient operation 取消测试使用的多结果集执行器。
    /// </summary>
    private sealed class OperationCancellationExecutor : SqlMultipleQueryExecutorBase
    {
        /// <summary>
        /// 初始化一个 <see cref="OperationCancellationExecutor" /> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">用于解析当前测试服务的服务提供程序。</param>
        public OperationCancellationExecutor(IServiceProvider serviceProvider)
            : base(serviceProvider, CreateOptions())
        {
        }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;

        /// <summary>
        /// 创建带数据库上下文的 SQL 测试选项。
        /// </summary>
        /// <returns>测试查询所使用的 SQL 选项。</returns>
        private static SqlOptions CreateOptions()
        {
            var options = new SqlOptions
            {
                DatabaseType = Bing.Data.Enums.DatabaseType.SqlServer
            };
            options.SetDatabaseContext(new DatabaseContext
            {
                DbKey = "operation-cancellation",
                DataSource = new SqlDataSourceDescriptor
                {
                    Key = "operation-cancellation",
                    DatabaseType = Bing.Data.Enums.DatabaseType.SqlServer,
                    ProviderKey = "test.operation-cancellation"
                }
            });
            return options;
        }
    }
}
