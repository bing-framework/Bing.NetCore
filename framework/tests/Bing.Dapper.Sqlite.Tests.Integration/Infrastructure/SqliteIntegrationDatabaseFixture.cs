using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQLite 集成测试数据库固定装置。
/// </summary>
public sealed class SqliteIntegrationDatabaseFixture : IAsyncLifetime, IAsyncDisposable
{
    private const string FirstDatabaseKey = "first";
    private const string SecondDatabaseKey = "second";
    private readonly string _directoryPath = Path.Combine(Path.GetTempPath(), "bing-framework-tests", "sqlite",
        Guid.NewGuid().ToString("N"));
    private ServiceProvider _serviceProvider;

    /// <summary>
    /// 获取第一个测试数据库文件路径。
    /// </summary>
    public string FirstDatabasePath { get; private set; }

    /// <summary>
    /// 获取第二个测试数据库文件路径。
    /// </summary>
    public string SecondDatabasePath { get; private set; }

    /// <summary>
    /// 获取第一个测试数据库连接字符串。
    /// </summary>
    public string FirstConnectionString { get; private set; }

    /// <summary>
    /// 获取第二个测试数据库连接字符串。
    /// </summary>
    public string SecondConnectionString { get; private set; }

    /// <summary>
    /// 获取测试服务提供程序。
    /// </summary>
    public IServiceProvider ServiceProvider => _serviceProvider ?? throw new ObjectDisposedException(nameof(SqliteIntegrationDatabaseFixture));

    /// <summary>
    /// 初始化 SQLite 测试数据库和服务容器。
    /// </summary>
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directoryPath);
        FirstDatabasePath = Path.Combine(_directoryPath, "first.db");
        SecondDatabasePath = Path.Combine(_directoryPath, "second.db");
        FirstConnectionString = CreateConnectionString(FirstDatabasePath);
        SecondConnectionString = CreateConnectionString(SecondDatabasePath);
        TestDatabase.DefaultConnectionString = FirstConnectionString;

        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, FirstConnectionString);
        services.AddSqlDataSource(FirstDatabaseKey, DatabaseType.Sqlite, FirstConnectionString,
            setupAction: descriptor => descriptor.MappingProfile = "first-profile");
        services.AddSqlDataSource(SecondDatabaseKey, DatabaseType.Sqlite, SecondConnectionString);
        services.AddSqliteSqlQuery();
        services.AddSqliteSqlExecutor();
        _serviceProvider = services.BuildServiceProvider();

        await InitializeDatabaseAsync(FirstConnectionString);
        await InitializeDatabaseAsync(SecondConnectionString);
    }

    /// <summary>
    /// 清理两个 SQLite 测试数据库中的样例数据。
    /// </summary>
    public async Task ResetAsync()
    {
        await ResetDatabaseAsync(FirstConnectionString);
        await ResetDatabaseAsync(SecondConnectionString);
    }

    /// <summary>
    /// 创建指定数据源的 SQL 查询对象。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>SQL 查询对象。</returns>
    public ISqlQuery CreateQuery(string dbKey = FirstDatabaseKey)
        => ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>(dbKey);

    /// <summary>
    /// 创建指定数据源的 SQL 执行对象。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>SQL 执行对象。</returns>
    public ISqlExecutor CreateExecutor(string dbKey = FirstDatabaseKey)
        => ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create<ISqlExecutor>(dbKey);

    /// <summary>
    /// 获取数据库作用域管理器。
    /// </summary>
    /// <returns>数据库作用域管理器。</returns>
    public IDatabaseScopeManager GetDatabaseScopeManager() => ServiceProvider.GetRequiredService<IDatabaseScopeManager>();

    /// <summary>
    /// 获取 SQL 事务作用域工厂。
    /// </summary>
    /// <returns>SQL 事务作用域工厂。</returns>
    public ISqlTransactionScopeFactory GetTransactionScopeFactory() =>
        ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();

    /// <summary>
    /// 读取指定数据库中的样例名称。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>样例名称集合。</returns>
    public async Task<List<string>> ReadNamesAsync(string dbKey = FirstDatabaseKey)
    {
        await using var connection = new SqliteConnection(GetConnectionString(dbKey));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Select Name From samples Order By Id";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
            names.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        return names;
    }

    /// <summary>
    /// 统计指定数据库中的样例记录数。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>样例记录数。</returns>
    public async Task<int> CountAsync(string dbKey = FirstDatabaseKey)
    {
        await using var connection = new SqliteConnection(GetConnectionString(dbKey));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Select Count(*) From samples";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 释放数据库、服务容器和临时文件。
    /// </summary>
    public Task DisposeAsync() => DisposeAsyncCore();

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsyncCore());

    /// <summary>
    /// 释放资源。
    /// </summary>
    /// <returns>释放任务。</returns>
    private Task DisposeAsyncCore()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directoryPath))
            Directory.Delete(_directoryPath, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 初始化指定 SQLite 数据库的表结构。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    /// <returns>初始化任务。</returns>
    private static async Task InitializeDatabaseAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
Create Table If Not Exists samples(
    Id Integer Primary Key Autoincrement,
    Name Text Null,
    Amount Decimal Null,
    SecretText Text Null
);";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 清理指定 SQLite 数据库的数据。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    /// <returns>清理任务。</returns>
    private static async Task ResetDatabaseAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Delete From samples";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 获取指定数据源的连接字符串。
    /// </summary>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>连接字符串。</returns>
    public string GetConnectionString(string dbKey)
    {
        if (string.Equals(dbKey, FirstDatabaseKey, StringComparison.OrdinalIgnoreCase))
            return FirstConnectionString;
        if (string.Equals(dbKey, SecondDatabaseKey, StringComparison.OrdinalIgnoreCase))
            return SecondConnectionString;
        throw new KeyNotFoundException($"未配置 SQLite 集成测试数据源: {dbKey ?? "<null>"}。");
    }

    /// <summary>
    /// 创建关闭连接池的 SQLite 文件连接字符串。
    /// </summary>
    /// <param name="databasePath">数据库文件路径。</param>
    /// <returns>连接字符串。</returns>
    private static string CreateConnectionString(string databasePath) =>
        $"Data Source={databasePath};Mode=ReadWriteCreate;Pooling=False";

    /// <summary>
    /// SQLite 集成测试数据库实现。
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        /// <summary>
        /// 默认 SQLite 测试数据库连接字符串。
        /// </summary>
        public static string DefaultConnectionString { get; set; }

        private readonly IDatabaseContextAccessor _databaseContextAccessor;

        /// <summary>
        /// 初始化一个<see cref="TestDatabase"/>类型的实例。
        /// </summary>
        /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
        public TestDatabase(IDatabaseContextAccessor databaseContextAccessor) =>
            _databaseContextAccessor = databaseContextAccessor;

        /// <summary>
        /// 获取当前数据源的 SQLite 连接。
        /// </summary>
        /// <returns>SQLite 连接。</returns>
        public System.Data.IDbConnection GetConnection()
        {
            var connectionString = _databaseContextAccessor.Current?.DataSource?.ConnectionString ??
                                   DefaultConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("测试数据库上下文缺少 SQLite 连接字符串。");
            return new SqliteConnection(connectionString);
        }
    }
}