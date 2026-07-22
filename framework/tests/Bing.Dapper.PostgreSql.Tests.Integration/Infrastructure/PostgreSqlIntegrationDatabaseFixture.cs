using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Dapper;
using Bing.Dapper.PostgreSql;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// PostgreSQL 集成测试数据库固定装置。
/// </summary>
public sealed class PostgreSqlIntegrationDatabaseFixture : IAsyncLifetime, IAsyncDisposable
{
    /// <summary>
    /// PostgreSQL 集成测试 Provider 标识。
    /// </summary>
    private const string Provider = "PostgreSql";

    /// <summary>
    /// 主数据源键。
    /// </summary>
    public const string PrimaryDatabaseKey = "primary";

    /// <summary>
    /// 报表数据源键。
    /// </summary>
    public const string ReportingDatabaseKey = "reporting";

    /// <summary>
    /// 集成测试根服务提供程序。
    /// </summary>
    private ServiceProvider _serviceProvider;

    /// <summary>
    /// 获取 PostgreSQL 专用测试数据库连接字符串。
    /// </summary>
    public string ConnectionString { get; private set; }

    /// <summary>
    /// 获取主数据源连接字符串。
    /// </summary>
    public string PrimaryConnectionString { get; private set; }

    /// <summary>
    /// 获取报表数据源连接字符串。
    /// </summary>
    public string ReportingConnectionString { get; private set; }

    /// <summary>
    /// 获取 PostgreSQL 集成测试服务提供程序。
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider ??
        throw new ObjectDisposedException(nameof(PostgreSqlIntegrationDatabaseFixture));

    /// <summary>
    /// 初始化 PostgreSQL 测试数据库和服务容器。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IntegrationTestGate.IsProviderEnabled(Provider) == false)
            return;
        ConnectionString = IntegrationTestConnectionStringResolver.Resolve(Provider);
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(ConnectionString, Provider);
        PrimaryConnectionString = CreateSchemaConnectionString(ConnectionString, "integration_primary");
        ReportingConnectionString = CreateSchemaConnectionString(ConnectionString, "integration_reporting");
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.InitializeAsync(connection);
        var services = new ServiceCollection();
        AddPostgreSqlIntegrationTestServices(services, PrimaryConnectionString, ReportingConnectionString);
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 注册 PostgreSQL 集成测试使用的服务，不建立或打开数据库连接。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="primaryConnectionString">主数据源连接字符串。</param>
    /// <param name="reportingConnectionString">报表数据源连接字符串。</param>
    /// <returns>已注册服务的集合。</returns>
    internal static IServiceCollection AddPostgreSqlIntegrationTestServices(IServiceCollection services,
        string primaryConnectionString, string reportingConnectionString)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSqlCore();
        services.AddPostgreSqlProvider();
        services.AddSqlDataSource(PrimaryDatabaseKey, DatabaseType.PgSql, primaryConnectionString);
        services.AddSqlDataSource(ReportingDatabaseKey, DatabaseType.PgSql, reportingConnectionString);
        services.AddLogging();
        return services;
    }

    /// <summary>
    /// 清理 PostgreSQL 专用测试表数据。
    /// </summary>
    /// <returns>异步清理任务。</returns>
    public async Task ResetAsync()
    {
        if (IntegrationTestGate.IsProviderEnabled(Provider) == false)
            return;
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(ConnectionString, Provider);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.ResetAsync(connection);
    }

    /// <summary>
    /// 创建指定数据源的 PostgreSQL SQL 查询对象。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>SQL 查询对象。</returns>
    public ISqlQuery CreateQuery(string dbKey = PrimaryDatabaseKey) =>
        ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>(dbKey);

    /// <summary>
    /// 创建指定数据源的 PostgreSQL SQL 执行对象。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>SQL 执行对象。</returns>
    public ISqlExecutor CreateExecutor(string dbKey = PrimaryDatabaseKey) =>
        ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create<ISqlExecutor>(dbKey);

    /// <summary>
    /// 获取数据库作用域管理器。
    /// </summary>
    /// <returns>数据库作用域管理器。</returns>
    public IDatabaseScopeManager GetDatabaseScopeManager() =>
        ServiceProvider.GetRequiredService<IDatabaseScopeManager>();

    /// <summary>
    /// 获取 SQL 事务作用域工厂。
    /// </summary>
    /// <returns>SQL 事务作用域工厂。</returns>
    public ISqlTransactionScopeFactory GetTransactionScopeFactory() =>
        ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();

    /// <summary>
    /// 获取指定数据源的连接字符串。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>指定数据源的连接字符串。</returns>
    public string GetConnectionString(string dbKey)
    {
        if (string.Equals(dbKey, PrimaryDatabaseKey, StringComparison.OrdinalIgnoreCase))
            return PrimaryConnectionString;
        if (string.Equals(dbKey, ReportingDatabaseKey, StringComparison.OrdinalIgnoreCase))
            return ReportingConnectionString;
        throw new KeyNotFoundException($"未配置 PostgreSQL 集成测试数据源: {dbKey ?? "<null>"}。");
    }

    /// <summary>
    /// 读取指定数据源中的路由样例名称。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>按标识排序的样例名称集合。</returns>
    public async Task<List<string>> ReadSampleNamesAsync(string dbKey)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString(dbKey));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("Select name From integration_samples Order By id", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }

    /// <summary>
    /// 释放服务容器和 PostgreSQL 连接池。
    /// </summary>
    public Task DisposeAsync() => DisposeAsyncCore();

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsyncCore());

    /// <summary>
    /// 释放 PostgreSQL 集成测试资源。
    /// </summary>
    /// <returns>异步释放任务。</returns>
    private async Task DisposeAsyncCore()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _serviceProvider?.Dispose();
        _serviceProvider = null;
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>
    /// 创建限定到指定 schema 的 PostgreSQL 连接字符串。
    /// </summary>
    /// <param name="connectionString">基础连接字符串。</param>
    /// <param name="schema">目标 schema 名称。</param>
    /// <returns>限定 schema 的连接字符串。</returns>
    private static string CreateSchemaConnectionString(string connectionString, string schema)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schema
        };
        return builder.ConnectionString;
    }
}