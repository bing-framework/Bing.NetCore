using System.IO;
using Bing.Data.Enums;
using Bing.Dapper;
using Bing.Dapper.Sqlite;

namespace Bing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// EF Core SQLite 多数据库真实执行集成测试。
/// </summary>
public sealed class EfCoreSqliteMultiDatabaseIntegrationTest : IAsyncLifetime
{
    private readonly string _directoryPath = Path.Combine(Path.GetTempPath(), "bing-framework-tests", "ef-sqlite",
        Guid.NewGuid().ToString("N"));
    private ServiceProvider _serviceProvider;
    private string _firstConnectionString;
    private string _secondConnectionString;

    /// <summary>
    /// 测试目的：Independent 模式应按 Ambient dbKey 在对应 SQLite 文件执行查询，显式 dbKey 应覆盖 Ambient。
    /// </summary>
    [Fact]
    public async Task Create_WhenIndependentUsesAmbientOrExplicitDbKey_ShouldExecuteAgainstResolvedFile()
    {
        // Arrange
        using var firstConnection = new SqliteConnection(_firstConnectionString);
        await firstConnection.OpenAsync();
        using var unitOfWork = CreateUnitOfWork(firstConnection);
        var factory = _serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var scopeManager = _serviceProvider.GetRequiredService<IDatabaseScopeManager>();

        // Act
        string ambientName;
        using (scopeManager.Use("second"))
        using (var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent))
            ambientName = query.AppendSelect("Name").AppendFrom("ef_file_users").AppendWhere("Id=1")
                .ExecuteScalar<string>();
        string explicitName;
        using (scopeManager.Use("second"))
        using (var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "first"))
            explicitName = query.AppendSelect("Name").AppendFrom("ef_file_users").AppendWhere("Id=1")
                .ExecuteScalar<string>();

        // Assert
        Assert.Equal("second-row", ambientName);
        Assert.Equal("first-row", explicitName);
    }

    /// <summary>
    /// 测试目的：Shared 模式使用同一 SQLite 文件时应复用 EF Core 连接并正常执行查询。
    /// </summary>
    [Fact]
    public async Task Create_WhenSharedUsesSameFile_ShouldReuseEfConnectionAndExecute()
    {
        // Arrange
        using var connection = new SqliteConnection(_firstConnectionString);
        await connection.OpenAsync();
        using var unitOfWork = CreateUnitOfWork(connection);
        var factory = _serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Shared, "first");
        var name = query.AppendSelect("Name").AppendFrom("ef_file_users").AppendWhere("Id=1")
            .ExecuteScalar<string>();

        // Assert
        Assert.Same(connection, query.GetConnection());
        Assert.Equal("first-row", name);
    }

    /// <summary>
    /// 测试目的：Shared 模式切换至不同 SQLite 文件时应在执行前失败，且 DbContext 仍可继续使用。
    /// </summary>
    [Fact]
    public async Task Create_WhenSharedTargetsDifferentFile_ShouldRejectAndKeepDbContextUsable()
    {
        // Arrange
        using var connection = new SqliteConnection(_firstConnectionString);
        await connection.OpenAsync();
        using var unitOfWork = CreateUnitOfWork(connection);
        var factory = _serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(unitOfWork, EfCoreSqlConnectionMode.Shared, "second"));
        await unitOfWork.Database.ExecuteSqlRawAsync(
            "Insert Into ef_file_users(Id, Name) Values (2, 'after-shared-rejection')");

        // Assert
        Assert.Contains("不同的物理数据库", exception.Message);
        Assert.Equal(2, await CountRowsAsync(_firstConnectionString));
        Assert.Equal(1, await CountRowsAsync(_secondConnectionString));
    }

    /// <summary>
    /// 测试目的：事务期间环境 dbKey 改变不应改变已创建 Shared Query 的连接与事务。
    /// </summary>
    [Fact]
    public async Task Create_WhenAmbientDbKeyChangesDuringTransaction_ShouldKeepSharedConnectionAndTransaction()
    {
        // Arrange
        using var connection = new SqliteConnection(_firstConnectionString);
        await connection.OpenAsync();
        using var unitOfWork = CreateUnitOfWork(connection);
        var factory = _serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var scopeManager = _serviceProvider.GetRequiredService<IDatabaseScopeManager>();
        using var transaction = await unitOfWork.Database.BeginTransactionAsync();
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Shared, "first");

        // Act
        string name;
        using (scopeManager.Use("second"))
            name = query.AppendSelect("Name").AppendFrom("ef_file_users").AppendWhere("Id=1")
                .ExecuteScalar<string>();

        // Assert
        Assert.Same(connection, query.GetConnection());
        Assert.Same(transaction.GetDbTransaction(), ((IDbTransactionManager)query).GetTransaction());
        Assert.Equal("first-row", name);
    }

    /// <summary>
    /// 测试目的：EF Core Shared 模式应接受 SQLite 构建器格式的命名共享内存身份，并在真实连接中读取相同数据库。
    /// </summary>
    [Fact]
    public async Task Create_WhenSharedUsesNamedMemoryBuilderConnection_ShouldReuseEfConnectionAndExecute()
    {
        // Arrange
        var connectionString = $"Data Source=ef-shared-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "Create Table shared_memory_users(Id Integer Primary Key, Name Text Not Null); Insert Into shared_memory_users(Id, Name) Values (1, 'shared-memory');";
            await command.ExecuteNonQueryAsync();
        }
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabase<Bing.Data.IDatabase, FileTestDatabase>();
        services.AddSqlDataSource("shared", DatabaseType.Sqlite, connectionString);
        services.AddSqliteProvider();
        services.AddEfCoreSqlQueryFactory();
        using var serviceProvider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<FileUnitOfWork>().UseSqlite(connection).Options;
        using var unitOfWork = new FileUnitOfWork(options, serviceProvider);

        // Act
        using var query = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>()
            .Create(unitOfWork, EfCoreSqlConnectionMode.Shared, "shared");
        var name = query.AppendSelect("Name").AppendFrom("shared_memory_users").AppendWhere("Id=1")
            .ExecuteScalar<string>();

        // Assert
        Assert.Same(connection, query.GetConnection());
        Assert.Equal("shared-memory", name);
    }

    /// <summary>
    /// 测试目的：不同名称的 SQLite 共享内存数据库不能在 EF Core Shared 模式复用同一连接。
    /// </summary>
    [Fact]
    public async Task Create_WhenSharedTargetsDifferentNamedMemory_ShouldRejectConnectionReuse()
    {
        // Arrange
        var currentConnectionString = $"Data Source=ef-current-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var targetConnectionString = $"Data Source=ef-target-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var connection = new SqliteConnection(currentConnectionString);
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabase<Bing.Data.IDatabase, FileTestDatabase>();
        services.AddSqlDataSource("target", DatabaseType.Sqlite, targetConnectionString);
        services.AddSqliteProvider();
        services.AddEfCoreSqlQueryFactory();
        using var serviceProvider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<FileUnitOfWork>().UseSqlite(connection).Options;
        using var unitOfWork = new FileUnitOfWork(options, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(unitOfWork, EfCoreSqlConnectionMode.Shared, "target"));

        // Assert
        Assert.Contains("不同的物理数据库", exception.Message);
        Assert.Same(connection, unitOfWork.Database.GetDbConnection());
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directoryPath);
        _firstConnectionString = CreateConnectionString(Path.Combine(_directoryPath, "first.db"));
        _secondConnectionString = CreateConnectionString(Path.Combine(_directoryPath, "second.db"));
        await InitializeDatabaseAsync(_firstConnectionString, "first-row");
        await InitializeDatabaseAsync(_secondConnectionString, "second-row");

        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = "first");
        services.AddDatabase<Bing.Data.IDatabase, FileTestDatabase>();
        services.AddSqlDataSource("first", DatabaseType.Sqlite, _firstConnectionString);
        services.AddSqlDataSource("second", DatabaseType.Sqlite, _secondConnectionString);
        services.AddSqliteProvider();
        services.AddEfCoreSqlQueryFactory();
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directoryPath))
            Directory.Delete(_directoryPath, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 创建使用第一个 SQLite 文件的 EF Core 工作单元。
    /// </summary>
    /// <param name="connection">EF Core 连接。</param>
    /// <returns>测试工作单元。</returns>
    private FileUnitOfWork CreateUnitOfWork(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<FileUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new FileUnitOfWork(options, _serviceProvider);
    }

    /// <summary>
    /// 初始化 SQLite 测试文件。
    /// </summary>
    /// <param name="connectionString">SQLite 连接字符串。</param>
    /// <param name="name">初始记录名称。</param>
    /// <returns>异步任务。</returns>
    private static async Task InitializeDatabaseAsync(string connectionString, string name)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
Create Table ef_file_users(
    Id Integer Primary Key,
    Name Text Not Null
);
Insert Into ef_file_users(Id, Name) Values (1, @name);";
        command.Parameters.AddWithValue("@name", name);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 统计 SQLite 文件中的测试记录。
    /// </summary>
    /// <param name="connectionString">SQLite 连接字符串。</param>
    /// <returns>记录数。</returns>
    private static async Task<int> CountRowsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "Select Count(*) From ef_file_users";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 创建关闭连接池的 SQLite 文件连接字符串。
    /// </summary>
    /// <param name="databasePath">数据库文件路径。</param>
    /// <returns>SQLite 连接字符串。</returns>
    private static string CreateConnectionString(string databasePath) =>
        $"Data Source={databasePath};Mode=ReadWriteCreate;Pooling=False";

    /// <summary>
    /// 测试工作单元。
    /// </summary>
    private sealed class FileUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个<see cref="FileUnitOfWork"/>类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public FileUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider) : base(options, serviceProvider)
        {
        }
    }

    /// <summary>
    /// 文件测试数据库实现。
    /// </summary>
    private sealed class FileTestDatabase : Bing.Data.IDatabase
    {
        private readonly IDatabaseContextAccessor _databaseContextAccessor;

        /// <summary>
        /// 初始化一个<see cref="FileTestDatabase"/>类型的实例。
        /// </summary>
        /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
        public FileTestDatabase(IDatabaseContextAccessor databaseContextAccessor) =>
            _databaseContextAccessor = databaseContextAccessor;

        /// <inheritdoc />
        public IDbConnection GetConnection()
        {
            var connectionString = _databaseContextAccessor.Current?.DataSource?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("文件测试数据库上下文缺少 SQLite 连接字符串。");
            return new SqliteConnection(connectionString);
        }
    }
}
