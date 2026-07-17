using Bing.Data.Sql;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// MySQL 集成测试数据库固定装置。
/// </summary>
public sealed class MySqlIntegrationDatabaseFixture : IAsyncLifetime, IAsyncDisposable
{
    private const string Provider = "MySql";
    private ServiceProvider _serviceProvider;
    /// <summary>
    /// 获取 MySQL 测试数据库连接字符串。
    /// </summary>
    public string ConnectionString { get; private set; }

    /// <summary>
    /// 获取 MySQL 集成测试服务提供程序。
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider ??
        throw new ObjectDisposedException(nameof(MySqlIntegrationDatabaseFixture));

    /// <summary>
    /// 初始化 MySQL 测试数据库。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IntegrationTestGate.IsProviderEnabled(Provider) == false)
            return;
        ConnectionString = IntegrationTestConnectionStringResolver.Resolve(Provider);
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(ConnectionString, Provider);
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.InitializeAsync(connection);
        var services = new ServiceCollection();
        Startup.ConfigureSqlServices(services, ConnectionString);
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 清理 MySQL 测试数据。
    /// </summary>
    public async Task ResetAsync()
    {
        if (IntegrationTestGate.IsProviderEnabled(Provider) == false)
            return;
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(ConnectionString, Provider);
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.ResetAsync(connection);
    }

    /// <summary>
    /// 创建 MySQL SQL 查询对象。
    /// </summary>
    /// <returns>SQL 查询对象。</returns>
    public ISqlQuery CreateQuery() => ServiceProvider.GetRequiredService<ISqlQuery>();

    /// <summary>
    /// 创建 MySQL SQL 执行对象。
    /// </summary>
    /// <returns>SQL 执行对象。</returns>
    public ISqlExecutor CreateExecutor() => ServiceProvider.GetRequiredService<ISqlExecutor>();

    /// <summary>
    /// 获取 SQL 事务作用域工厂。
    /// </summary>
    /// <returns>SQL 事务作用域工厂。</returns>
    public ISqlTransactionScopeFactory GetTransactionScopeFactory() =>
        ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();

    /// <summary>
    /// 释放资源。
    /// </summary>
    public Task DisposeAsync() => DisposeAsyncCore();

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsyncCore());

    /// <summary>
    /// 异步释放服务提供程序。
    /// </summary>
    /// <returns>释放任务。</returns>
    private async Task DisposeAsyncCore()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _serviceProvider?.Dispose();
        _serviceProvider = null;
    }
}