using Bing.Data.Enums;
using Bing.Data.Sql.Configs;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// EF Core SQL 查询工厂测试
/// </summary>
public class EfCoreSqlQueryFactoryTest
{
    /// <summary>
    /// 测试目的：Shared 模式应复用 DbContext 连接，并使用 EF Core 模型映射生成 SQL。
    /// </summary>
    [Fact]
    public void Create_WhenShared_ShouldUseDbContextConnectionAndModelMapping()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var query = factory.Create(unitOfWork);
        query.From<TestEntity>().Where<TestEntity>(entity => entity.DisplayName, "Bing");
        var sql = query.GetDebugSql();

        // Assert
        Assert.Same(connection, query.GetConnection());
        Assert.Contains("ef_query_users", sql);
        Assert.Contains("display_name", sql);
    }

    /// <summary>
    /// 测试目的：创建 Query 后开启 EF Core 事务时，Shared Query 应动态返回该事务。
    /// </summary>
    [Fact]
    public void Create_WhenEfTransactionStartsAfterQueryCreation_ShouldResolveCurrentTransaction()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var query = factory.Create(unitOfWork);
        var transactionManager = (IDbTransactionManager)query;

        // Act
        using var transaction = unitOfWork.Database.BeginTransaction();

        // Assert
        Assert.Same(transaction.GetDbTransaction(), transactionManager.GetTransaction());
    }

    /// <summary>
    /// 测试目的：Independent 模式应使用当前 DbContext Provider 和连接字符串创建独立连接，且不共享 EF 事务。
    /// </summary>
    [Fact]
    public void Create_WhenIndependent_ShouldUseSeparateProviderConnection()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent);

        // Assert
        var independentConnection = query.GetConnection();
        Assert.IsType<SqliteConnection>(independentConnection);
        Assert.NotSame(connection, independentConnection);
        Assert.Equal(connection.ConnectionString, independentConnection.ConnectionString);
        Assert.Null(((IDbTransactionManager)query).GetTransaction());
    }

    /// <summary>
    /// 测试目的：同一 Provider 存在多个 SQL 数据源且未显式指定 dbKey 时，应抛出明确异常而不是取第一个。
    /// </summary>
    [Fact]
    public void Create_WhenMultipleMatchingDataSourcesWithoutDbKey_ShouldThrow()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create(unitOfWork));

        // Assert
        Assert.Contains("请显式指定 dbKey", exception.Message);
    }

    /// <summary>
    /// 测试目的：Independent 模式显式指定 dbKey 时，应使用目标数据源的连接字符串创建独立连接。
    /// </summary>
    [Fact]
    public void Create_WhenIndependentWithDbKey_ShouldUseSelectedDataSourceConnectionString()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "reporting");

        // Assert
        var independentConnection = Assert.IsType<SqliteConnection>(query.GetConnection());
        Assert.Equal("Data Source=reporting.db", independentConnection.ConnectionString);
    }

    /// <summary>
    /// 测试目的：Independent 模式应通过目标数据源的命名连接字符串创建连接。
    /// </summary>
    [Fact]
    public void Create_WhenIndependentDataSourceUsesConnectionStringName_ShouldUseNamedConnection()
    {
        // Arrange
        var metadataOptions = CreateMetadataOptions();
        var reporting = metadataOptions.DataSources.DataSources["reporting"];
        reporting.ConnectionString = null;
        reporting.ConnectionStringName = "ReportingConnection";
        var connectionStrings = new ConnectionStringCollection
        {
            ["ReportingConnection"] = "Data Source=named-reporting.db"
        };
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider(metadataOptions, connectionStrings);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "reporting");

        // Assert
        var independentConnection = Assert.IsType<SqliteConnection>(query.GetConnection());
        Assert.Equal("Data Source=named-reporting.db", independentConnection.ConnectionString);
    }

    /// <summary>
    /// 测试目的：Independent 模式找不到数据源命名连接时必须失败，不能回退到 DbContext 连接。
    /// </summary>
    [Fact]
    public void Create_WhenIndependentNamedConnectionIsMissing_ShouldNotFallbackToDbContextConnection()
    {
        // Arrange
        var metadataOptions = CreateMetadataOptions();
        var reporting = metadataOptions.DataSources.DataSources["reporting"];
        reporting.ConnectionString = null;
        reporting.ConnectionStringName = "MissingReportingConnection";
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider(metadataOptions);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "reporting"));

        // Assert
        Assert.Contains("reporting", exception.Message);
        Assert.Contains("MissingReportingConnection", exception.Message);
        Assert.DoesNotContain(connection.ConnectionString, exception.Message);
    }

    /// <summary>
    /// 测试目的：显式指定的 dbKey 与 EF Core Provider 类型不一致时，应抛出明确异常。
    /// </summary>
    [Fact]
    public void Create_WhenDbKeyProviderMismatch_ShouldThrow()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "sqlserver"));

        // Assert
        Assert.Contains("不一致", exception.Message);
    }

    /// <summary>
    /// 测试目的：Shared 模式显式指定不同物理数据库时必须拒绝复用 DbContext 连接。
    /// </summary>
    [Fact]
    public void Create_WhenSharedDbKeyTargetsDifferentPhysicalDatabase_ShouldThrow()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(unitOfWork, EfCoreSqlConnectionMode.Shared, "reporting"));

        // Assert
        Assert.Contains("不同的物理数据库", exception.Message);
        Assert.Contains(nameof(EfCoreSqlConnectionMode.Independent), exception.Message);
        Assert.Same(connection, unitOfWork.Database.GetDbConnection());
    }

    /// <summary>
    /// 创建服务提供程序
    /// </summary>
    /// <returns>服务提供程序</returns>
    private static ServiceProvider CreateServiceProvider(SqlMetadataOptions metadataOptions = null,
        ConnectionStringCollection connectionStrings = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (connectionStrings != null)
            services.AddSingleton(connectionStrings);
        if (metadataOptions != null)
            services.ConfigureSqlMetadata(options => ApplyMetadataOptions(options, metadataOptions));
        services.AddDatabase<Bing.Data.IDatabase, TestDatabase>();
        services.AddSqliteSqlQuery("Data Source=:memory:");
        services.AddEfCoreSqlQueryFactory();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建测试元数据配置
    /// </summary>
    /// <returns>Sql 元数据配置</returns>
    private static SqlMetadataOptions CreateMetadataOptions()
    {
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = "Data Source=reporting.db"
        };
        options.DataSources.DataSources["sqlserver"] = new SqlDataSourceDescriptor
        {
            Key = "sqlserver",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=sqlserver;Database=test;"
        };
        return options;
    }

    /// <summary>
    /// 应用测试元数据配置
    /// </summary>
    /// <param name="target">目标配置</param>
    /// <param name="source">源配置</param>
    private static void ApplyMetadataOptions(SqlMetadataOptions target, SqlMetadataOptions source)
    {
        if (target == null || source == null)
            return;
        target.DataSources.DefaultDataSourceKey = source.DataSources.DefaultDataSourceKey;
        foreach (var dataSource in source.DataSources.DataSources.Values)
        {
            target.DataSources.DataSources[dataSource.Key] = new SqlDataSourceDescriptor
            {
                Key = dataSource.Key,
                DatabaseType = dataSource.DatabaseType,
                ConnectionString = dataSource.ConnectionString,
                ConnectionStringName = dataSource.ConnectionStringName,
                IsReadOnly = dataSource.IsReadOnly,
                MappingProfile = dataSource.MappingProfile,
                PrimaryReadStrategy = dataSource.PrimaryReadStrategy,
                PrimaryDataSourceKey = dataSource.PrimaryDataSourceKey
            };
        }
    }

    /// <summary>
    /// 创建测试工作单元
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <returns>测试工作单元</returns>
    private static TestUnitOfWork CreateUnitOfWork(SqliteConnection connection, IServiceProvider serviceProvider)
    {
        var options = new DbContextOptionsBuilder<TestUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new TestUnitOfWork(options, serviceProvider);
    }

    /// <summary>
    /// 测试工作单元
    /// </summary>
    private sealed class TestUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个<see cref="TestUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">数据库上下文配置</param>
        /// <param name="serviceProvider">服务提供程序</param>
        public TestUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 配置实体映射
        /// </summary>
        /// <param name="modelBuilder">模型生成器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(builder =>
            {
                builder.ToTable("ef_query_users");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.DisplayName).HasColumnName("display_name");
            });
        }
    }

    /// <summary>
    /// 测试数据库
    /// </summary>
    private sealed class TestDatabase : Bing.Data.IDatabase
    {
        /// <inheritdoc />
        public IDbConnection GetConnection() => new SqliteConnection("Data Source=:memory:");
    }

    /// <summary>
    /// 测试实体
    /// </summary>
    private sealed class TestEntity
    {
        /// <summary>
        /// 标识
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }
    }
}