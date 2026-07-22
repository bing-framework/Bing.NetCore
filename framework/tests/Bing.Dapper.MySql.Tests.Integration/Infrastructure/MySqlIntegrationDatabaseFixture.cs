using AspectCore.Extensions.DependencyInjection;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Bing.DependencyInjection;
using Bing.Test.Shared;
using Bing.Tests.Models;
using Bing.Tests.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// MySQL 集成测试数据库固定装置。
/// </summary>
public sealed class MySqlIntegrationDatabaseFixture : IAsyncLifetime, IAsyncDisposable
{
    /// <summary>
    /// MySQL 集成测试 Provider 标识。
    /// </summary>
    private const string Provider = "MySql";

    /// <summary>
    /// 集成测试根服务提供程序。
    /// </summary>
    private ServiceProvider _serviceProvider;

    /// <summary>
    /// 已创建的根服务提供程序数量。
    /// </summary>
    public int RootServiceProviderCreationCount { get; private set; }

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
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(ConnectionString, Provider);
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.InitializeAsync(connection);
        var services = new ServiceCollection();
        AddMySqlIntegrationTestServices(services, ConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        RootServiceProviderCreationCount++;
    }

    /// <summary>
    /// 注册 MySQL 集成测试使用的服务，不建立或打开数据库连接。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="connectionString">MySQL 连接字符串。</param>
    /// <returns>已注册服务的集合。</returns>
    internal static IServiceCollection AddMySqlIntegrationTestServices(IServiceCollection services,
        string connectionString)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddSingleton<IEntityModelMetadataProvider>(provider =>
            CreateEntityModelMetadataProvider(connectionString, provider));
        services.AddSqlCore();
        services.AddMySqlQuery(connectionString);
        services.AddMySqlExecutor(connectionString);
        services.AddLogging();
        services.EnableAop();
        services.AddBing();
        return services;
    }

    /// <summary>
    /// 创建 MySQL EF 实体模型元数据提供器，不探测或连接数据库。
    /// </summary>
    /// <param name="connectionString">MySQL 连接字符串。</param>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <returns>实体模型元数据提供器。</returns>
    private static IEntityModelMetadataProvider CreateEntityModelMetadataProvider(string connectionString,
        IServiceProvider serviceProvider)
    {
        var options = new DbContextOptionsBuilder<IntegrationMySqlUnitOfWork>()
            .UseMySql(connectionString, ServerVersion.Parse("8.0.0"))
            .Options;
        return new IntegrationMySqlUnitOfWork(options, serviceProvider);
    }

    /// <summary>
    /// MySQL 集成测试专用的实体模型工作单元。
    /// </summary>
    private sealed class IntegrationMySqlUnitOfWork : MySqlUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="IntegrationMySqlUnitOfWork"/>类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public IntegrationMySqlUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Product>().Property(entity => entity.Id).HasColumnName("ProductId");
        }
    }

    /// <summary>
    /// 清理 MySQL 测试数据。
    /// </summary>
    public async Task ResetAsync()
    {
        if (IntegrationTestGate.IsProviderEnabled(Provider) == false)
            return;
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(ConnectionString, Provider);
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await DatabaseScript.ResetAsync(connection);
    }

    /// <summary>
    /// 创建 MySQL SQL 查询对象。
    /// </summary>
    /// <returns>SQL 查询对象。</returns>
    public ISqlQuery CreateQuery() => GetQueryFactory().Create<ISqlQuery>();

    /// <summary>
    /// 创建 MySQL SQL 执行对象。
    /// </summary>
    /// <returns>SQL 执行对象。</returns>
    public ISqlExecutor CreateExecutor() => GetExecutorFactory().Create<ISqlExecutor>();

    /// <summary>
    /// 获取 SQL 查询工厂。
    /// </summary>
    /// <returns>SQL 查询工厂。</returns>
    public ISqlQueryFactory GetQueryFactory() => ServiceProvider.GetRequiredService<ISqlQueryFactory>();

    /// <summary>
    /// 获取 SQL 执行器工厂。
    /// </summary>
    /// <returns>SQL 执行器工厂。</returns>
    public ISqlExecutorFactory GetExecutorFactory() => ServiceProvider.GetRequiredService<ISqlExecutorFactory>();

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
        MySqlConnection.ClearAllPools();
    }
}