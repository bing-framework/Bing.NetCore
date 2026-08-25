using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// Sqlite 多数据源执行测试。
/// </summary>
public class SqliteMultiDataSourceExecutionTest
{
    /// <summary>
    /// 测试目的：切换具名数据源后，执行器应向各自的 SQLite 文件写入数据。
    /// </summary>
    [Fact]
    public void Execute_WhenNamedDataSourceChanges_ShouldWriteToMatchingSqliteFile()
    {
        // Arrange
        var firstPath = CreateDatabasePath();
        var secondPath = CreateDatabasePath();
        try
        {
            using var provider = CreateProvider(firstPath, secondPath);
            var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
            var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();

            // Act
            using (databaseScopeManager.Use("first"))
                CreateTableAndInsert(executorFactory, "first");
            using (databaseScopeManager.Use("second"))
                CreateTableAndInsert(executorFactory, "second");

            // Assert
            Assert.Equal("first", ReadName(firstPath));
            Assert.Equal("second", ReadName(secondPath));
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    /// <summary>
    /// 测试目的：事务开始后即使切换环境数据源，事务执行器仍应写入开始时捕获的 SQLite 文件。
    /// </summary>
    [Fact]
    public void TransactionScope_WhenAmbientDataSourceChanges_ShouldUseCapturedSqliteFile()
    {
        // Arrange
        var firstPath = CreateDatabasePath();
        var secondPath = CreateDatabasePath();
        try
        {
            using var provider = CreateProvider(firstPath, secondPath);
            var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
            var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();
            var transactionScopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();
            using (databaseScopeManager.Use("first"))
                CreateTable(executorFactory);
            using (databaseScopeManager.Use("second"))
                CreateTable(executorFactory);

            // Act
            using (databaseScopeManager.Use("first"))
            using (var transactionScope = transactionScopeFactory.Begin())
            {
                using (databaseScopeManager.Use("second"))
                {
                    using var executor = transactionScope.CreateExecutor();
                    executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "captured" });
                }
                transactionScope.Commit();
            }

            // Assert
            Assert.Equal("captured", ReadName(firstPath));
            Assert.Null(ReadName(secondPath));
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    /// <summary>
    /// 测试 - buffered=false 列表查询应保留非缓冲执行语义并完成结果物化。
    /// </summary>
    [Fact]
    public void ExecuteQuery_WhenBufferedIsFalse_ShouldMaterializeSqliteRows()
    {
        // Arrange
        var firstPath = CreateDatabasePath();
        var secondPath = CreateDatabasePath();
        try
        {
            using var provider = CreateProvider(firstPath, secondPath);
            var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
            var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();
            var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
            using (databaseScopeManager.Use("first"))
            {
                CreateTableAndInsert(executorFactory, "first");
                using var query = queryFactory.Create();
                // Act
                var result = query.Query().Select("Id,Name").From("samples").AsEnumerable<Sample>().ToList();

                // Assert
                Assert.Single(result);
                Assert.Equal("first", result[0].Name);
            }
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    /// <summary>
    /// 测试 - 提前终止 SQLite 流式枚举后，应能立即继续在同一数据源执行写操作。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenEnumerationStopsEarly_ShouldReleaseSqliteReader()
    {
        // Arrange
        var firstPath = CreateDatabasePath();
        var secondPath = CreateDatabasePath();
        try
        {
            using var provider = CreateProvider(firstPath, secondPath);
            var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
            var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();
            var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
            using (databaseScopeManager.Use("first"))
            {
                CreateTableAndInsert(executorFactory, "first");
                using (var query = queryFactory.Create())
                {
                    using var enumerator = query.Query().Select("Id,Name").From("samples")
                        .AsEnumerable<Sample>().GetEnumerator();
                    Assert.True(enumerator.MoveNext());
                    Assert.Equal("first", enumerator.Current.Name);
                }

                // Act
                using var executor = executorFactory.Create();
                executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "after-stream" });

                // Assert
                Assert.Equal(2, CountRows(firstPath));
            }
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    /// <summary>
    /// 测试 - 取消异步 SQLite 流后，应释放读取器并允许继续写入同一数据源。
    /// </summary>
    [Fact]
    public async Task StreamAsync_WhenCancellationRequested_ShouldReleaseReaderAndAllowSubsequentWrite()
    {
        // Arrange
        var firstPath = CreateDatabasePath();
        var secondPath = CreateDatabasePath();
        try
        {
            using var provider = CreateProvider(firstPath, secondPath);
            var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
            var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();
            var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
            using (databaseScopeManager.Use("first"))
            {
                CreateTableAndInsert(executorFactory, "first");
                Insert(executorFactory, "second-row");
                using var cancellationTokenSource = new CancellationTokenSource();
                using (var query = queryFactory.Create())
                {
                    // Act
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    {
                        await foreach (var _ in query.Query().Select("Id,Name").From("samples")
                                           .AsAsyncEnumerable<Sample>(cancellationToken: cancellationTokenSource.Token))
                            cancellationTokenSource.Cancel();
                    });
                }

                using var executor = executorFactory.Create();
                executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = "after-cancellation" });

                // Assert
                Assert.Equal(3, CountRows(firstPath));
            }
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    /// <summary>
    /// 测试 - Task.WhenAll 下两个 SQLite 数据源的查询应相互隔离。
    /// </summary>
    [Fact]
    public async Task ExecuteQuery_WhenParallelDatabaseScopesRun_ShouldKeepSqliteResultsIsolated()
    {
        // Arrange
        var firstPath = CreateDatabasePath();
        var secondPath = CreateDatabasePath();
        try
        {
            using var provider = CreateProvider(firstPath, secondPath);
            var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
            var executorFactory = provider.GetRequiredService<ISqlExecutorFactory>();
            var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
            using (databaseScopeManager.Use("first"))
                CreateTableAndInsert(executorFactory, "first");
            using (databaseScopeManager.Use("second"))
                CreateTableAndInsert(executorFactory, "second");

            // Act
            var names = await Task.WhenAll(
                QueryNameAsync(databaseScopeManager, queryFactory, "first"),
                QueryNameAsync(databaseScopeManager, queryFactory, "second"));

            // Assert
            Assert.Contains("first", names);
            Assert.Contains("second", names);
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    /// <summary>
    /// 创建测试服务提供程序。
    /// </summary>
    /// <param name="firstPath">第一个 SQLite 文件路径。</param>
    /// <param name="secondPath">第二个 SQLite 文件路径。</param>
    /// <returns>服务提供程序。</returns>
    private static ServiceProvider CreateProvider(string firstPath, string secondPath)
    {
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("first", DatabaseType.Sqlite, $"Data Source={firstPath};Pooling=False");
        services.AddSqlDataSource("second", DatabaseType.Sqlite, $"Data Source={secondPath};Pooling=False");
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建数据表并插入样例数据。
    /// </summary>
    /// <param name="executorFactory">SQL 执行器工厂。</param>
    /// <param name="name">样例名称。</param>
    private static void CreateTableAndInsert(ISqlExecutorFactory executorFactory, string name)
    {
        CreateTable(executorFactory);
        Insert(executorFactory, name);
    }

    /// <summary>
    /// 插入样例数据。
    /// </summary>
    /// <param name="executorFactory">SQL 执行器工厂。</param>
    /// <param name="name">样例名称。</param>
    private static void Insert(ISqlExecutorFactory executorFactory, string name)
    {
        using var executor = executorFactory.Create();
        executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name });
    }

    /// <summary>
    /// 创建数据表。
    /// </summary>
    /// <param name="executorFactory">SQL 执行器工厂。</param>
    private static void CreateTable(ISqlExecutorFactory executorFactory)
    {
        using var executor = executorFactory.Create();
        executor.ExecuteSql("Create Table samples(Id Integer Primary Key, Name Text)");
    }

    /// <summary>
    /// 读取 SQLite 文件中的样例名称。
    /// </summary>
    /// <param name="path">SQLite 文件路径。</param>
    /// <returns>样例名称。</returns>
    private static string ReadName(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "Select Name From samples Limit 1";
        return command.ExecuteScalar() as string;
    }

    /// <summary>
    /// 统计 SQLite 文件中的样例记录数。
    /// </summary>
    /// <param name="path">SQLite 文件路径。</param>
    /// <returns>样例记录数。</returns>
    private static int CountRows(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "Select Count(*) From samples";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>
    /// 在指定数据库作用域中异步查询样例名称。
    /// </summary>
    /// <param name="databaseScopeManager">数据库作用域管理器。</param>
    /// <param name="queryFactory">SQL 查询工厂。</param>
    /// <param name="dbKey">数据源标识。</param>
    /// <returns>样例名称。</returns>
    private static async Task<string> QueryNameAsync(IDatabaseScopeManager databaseScopeManager,
        ISqlQueryFactory queryFactory, string dbKey)
    {
        await Task.Yield();
        using (databaseScopeManager.Use(dbKey))
        using (var query = queryFactory.Create())
        {
            return query.Query().Select("Name").From("samples").AsEnumerable<Sample>().Single().Name;
        }
    }

    /// <summary>
    /// 创建临时 SQLite 文件路径。
    /// </summary>
    /// <returns>临时 SQLite 文件路径。</returns>
    private static string CreateDatabasePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bing-sqlite-{Guid.NewGuid():N}.db");
        return path;
    }

    /// <summary>
    /// 删除临时 SQLite 文件。
    /// </summary>
    /// <param name="path">SQLite 文件路径。</param>
    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// 测试数据库。
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        /// <summary>
        /// 数据库上下文访问器。
        /// </summary>
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
            var connectionString = _databaseContextAccessor.Current?.DataSource?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("测试数据库上下文缺少 SQLite 连接字符串。");
            return new SqliteConnection(connectionString);
        }
    }

    /// <summary>
    /// SQLite 样例实体。
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}
