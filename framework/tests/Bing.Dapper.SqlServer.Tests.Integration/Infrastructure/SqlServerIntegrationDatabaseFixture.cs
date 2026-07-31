using Bing.Dapper.SqlServer;
using Bing.Data.Sql;
using Bing.DependencyInjection;
using Bing.Test.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQL Server 聚合集成测试数据库固定装置。
/// </summary>
public sealed class SqlServerIntegrationDatabaseFixture : IAsyncLifetime, IAsyncDisposable
{
    /// <summary>
    /// SQL Server 集成测试 Provider 标识。
    /// </summary>
    private const string Provider = "SqlServer";

    /// <summary>
    /// 集成测试根服务提供程序。
    /// </summary>
    private ServiceProvider _serviceProvider;

    /// <summary>
    /// 获取 SQL Server 测试数据库连接字符串。
    /// </summary>
    public string ConnectionString { get; private set; }

    /// <summary>
    /// 获取 SQL Server 集成测试服务提供程序。
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider ??
        throw new ObjectDisposedException(nameof(SqlServerIntegrationDatabaseFixture));

    /// <summary>
    /// 初始化受控 SQL Server 测试表和查询服务。
    /// </summary>
    /// <returns>异步任务。</returns>
    public async Task InitializeAsync()
    {
        if (IntegrationTestGate.IsProviderEnabled(Provider) == false)
            return;
        ConnectionString = IntegrationTestConnectionStringResolver.Resolve(Provider);
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(ConnectionString, Provider);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.InitializeAsync(connection);

        var services = new ServiceCollection();
        services.AddSqlServerSqlQuery(ConnectionString);
        services.AddSqlServerSqlExecutor(ConnectionString);
        services.AddLogging();
        services.AddBing();
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 清空受控 SQL Server 聚合集成测试表。
    /// </summary>
    /// <returns>异步任务。</returns>
    public async Task ResetAsync()
    {
        if (IntegrationTestGate.IsProviderEnabled(Provider) == false)
            return;
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(ConnectionString, Provider);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.ResetAsync(connection);
    }

    /// <summary>
    /// 写入受控 SQL Server 聚合测试数据。
    /// </summary>
    /// <returns>异步任务。</returns>
    public async Task SeedAggregateDataAsync()
    {
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(ConnectionString, Provider);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.SeedAggregateDataAsync(connection);
    }

    /// <summary>
    /// 创建 SQL Server 查询对象。
    /// </summary>
    /// <returns>新的 SQL 查询对象。</returns>
    public ISqlQuery CreateQuery() => ServiceProvider.GetRequiredService<ISqlQuery>();

    /// <summary>
    /// 创建 SQL Server 执行器。
    /// </summary>
    /// <returns>新的 SQL 执行器。</returns>
    public ISqlExecutor CreateExecutor() => ServiceProvider.GetRequiredService<ISqlExecutor>();

    /// <summary>
    /// 释放服务提供程序和 SQL Server 连接池。
    /// </summary>
    /// <returns>异步任务。</returns>
    public Task DisposeAsync() => DisposeAsyncCore();

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsyncCore());

    /// <summary>
    /// 执行异步释放。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task DisposeAsyncCore()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _serviceProvider?.Dispose();
        _serviceProvider = null;
        SqlConnection.ClearAllPools();
    }
}