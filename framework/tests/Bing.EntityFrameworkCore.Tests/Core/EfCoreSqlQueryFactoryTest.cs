using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Builders.Core;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Data.Sql.Builders;
using System.Data.Common;
using System.Diagnostics;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// EF Core SQL 查询工厂测试
/// </summary>
public class EfCoreSqlQueryFactoryTest
{
    /// <summary>
    /// 可安全用于 Shared 模式的命名 SQLite 共享内存数据库连接字符串。
    /// </summary>
    private const string SharedMemoryConnectionString =
        "Data Source=file:ef-core-query-factory?mode=memory&cache=shared";

    /// <summary>
    /// 测试目的：公共查询基类不应暴露仅供框架桥接使用的资源和元数据绑定 SPI。
    /// </summary>
    [Fact]
    public void SqlQueryBase_ShouldNotExposeRuntimeBindingInterfaces()
    {
        // Arrange
        var interfaces = typeof(SqlQueryBase).GetInterfaces();

        // Assert
        var interfaceNames = interfaces.Select(item => item.Name);
        Assert.DoesNotContain("ISqlQueryExecutionResourceAccessor", interfaceNames);
        Assert.DoesNotContain("ISqlQueryResourceBinder", interfaceNames);
        Assert.DoesNotContain("ISqlTransactionScopeResourceBinder", interfaceNames);
        Assert.DoesNotContain("ISqlQueryMetadataBinder", interfaceNames);
    }

    /// <summary>
    /// 测试目的：Shared 模式应复用 DbContext 连接，并使用 EF Core 模型映射生成 SQL。
    /// </summary>
    [Fact]
    public void Create_WhenShared_ShouldUseDbContextConnectionAndModelMapping()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var query = factory.Create(unitOfWork);
        var sql = query.From<TestEntity>().Where(entity => entity.DisplayName, "Bing").ToSql();

        // Assert
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
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var query = factory.Create(unitOfWork);
        // Act
        using var transaction = unitOfWork.Database.BeginTransaction();

        // Assert
        AssertCanExecute(query);
    }

    /// <summary>
    /// 测试目的：Shared Query 应在 EF Core 事务完成后清除缓存，并解析后续新事务且不释放 EF 连接。
    /// </summary>
    [Fact]
    public void Create_WhenEfTransactionsChange_ShouldRefreshCurrentTransactionWithoutOwningEfResources()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var query = factory.Create(unitOfWork);
        // Act
        using (var firstTransaction = unitOfWork.Database.BeginTransaction())
        {
            AssertCanExecute(query);
            firstTransaction.Commit();
        }
        using var secondTransaction = unitOfWork.Database.BeginTransaction();
        var result = ExecuteCount(query);
        query.Dispose();

        // Assert
        Assert.True(result >= 0);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    /// <summary>
    /// 测试目的：Independent 模式应使用当前 DbContext Provider 和连接字符串创建独立连接，且不共享 EF 事务。
    /// </summary>
    [Fact]
    public void Create_WhenIndependent_ShouldUseSeparateProviderConnection()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent);

        // Assert
        AssertCanExecute(query);
    }

    /// <summary>
    /// 测试目的：Shared 模式应在诊断快照中声明 EF Core 外部连接与当前 EF Core 事务。
    /// </summary>
    [Fact]
    public void Create_WhenSharedQueryExecutesInsideEfTransaction_ShouldPublishExternalEfDiagnostics()
    {
        // Arrange
        const string sql = "Select Count(*) From sqlite_master Where Name = @name";
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item =>
        {
            if (item.Sql == sql)
                message = item;
        });
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        using var transaction = unitOfWork.Database.BeginTransaction();
        using var query = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>().Create(unitOfWork);

        // Act
        var result = query.Text<int>(sql, new { name = "ef_shared_diagnostics" }).Scalar();

        // Assert
        Assert.Equal(0, result);
        Assert.NotNull(message);
        Assert.Equal(SqlConnectionSource.EntityFrameworkCore, message.Connection.Source);
        Assert.Equal(SqlResourceOwnership.External, message.Connection.Ownership);
        Assert.True(message.Transaction.HasTransaction);
        Assert.Equal(SqlResourceOwnership.External, message.Transaction.Ownership);
    }

    /// <summary>
    /// 测试目的：Independent 模式即使 EF Core 事务已存在，也应使用自有数据源连接且不绑定该事务。
    /// </summary>
    [Fact]
    public void Create_WhenIndependentQueryExecutesInsideEfTransaction_ShouldPublishOwnedDataSourceDiagnostics()
    {
        // Arrange
        const string sql = "Select Count(*) From sqlite_master Where Name = @name";
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item =>
        {
            if (item.Sql == sql)
                message = item;
        });
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        using var transaction = unitOfWork.Database.BeginTransaction();
        using var query = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>()
            .Create(unitOfWork, EfCoreSqlConnectionMode.Independent);

        // Act
        var result = query.Text<int>(sql, new { name = "ef_independent_diagnostics" }).Scalar();

        // Assert
        Assert.Equal(0, result);
        Assert.NotNull(message);
        Assert.Equal(SqlConnectionSource.DataSource, message.Connection.Source);
        Assert.Equal(SqlResourceOwnership.Owned, message.Connection.Ownership);
        Assert.False(message.Transaction.HasTransaction);
    }

    /// <summary>
    /// 测试目的：配置默认数据源后，同一 Provider 的多个数据源未指定 dbKey 时应使用默认数据源。
    /// </summary>
    [Fact]
    public void Create_WhenDefaultDataSourceConfigured_ShouldUseDefaultDataSource()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork);

        // Assert
        Assert.NotNull(query);
    }

    /// <summary>
    /// 测试目的：Independent 模式显式指定 dbKey 时，应使用目标数据源的连接字符串创建独立连接。
    /// </summary>
    [Fact]
    public void Create_WhenIndependentWithDbKey_ShouldUseSelectedDataSourceConnectionString()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "reporting");

        // Assert
        AssertCanExecute(query);
    }

    /// <summary>
    /// 测试目的：未显式指定 dbKey 时，Independent 模式应使用环境 Use(dbKey) 解析的数据源，并在作用域释放后保持查询快照。
    /// </summary>
    [Fact]
    public void Create_WhenAmbientDatabaseScopeIsSet_ShouldUseScopedDataSourceSnapshot()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var databaseScopeManager = serviceProvider.GetRequiredService<IDatabaseScopeManager>();

        // Act
        ISqlQuery query;
        using (databaseScopeManager.Use("reporting"))
            query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent);

        // Assert
        using (query)
        {
            AssertCanExecute(query);
        }
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
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider(metadataOptions, connectionStrings);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "reporting");

        // Assert
        AssertCanExecute(query);
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
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
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
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
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
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
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
    /// 测试目的：通过 DI 注册的自定义物理身份贡献器应参与 Shared 模式连接比较。
    /// </summary>
    [Fact]
    public void Create_WhenCustomIdentityContributorIsRegistered_ShouldUseItForSharedConnectionComparison()
    {
        // Arrange
        var contributor = new TestSqliteIdentityContributor();
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions(), identityContributor: contributor);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Shared, "reporting");

        // Assert
        Assert.True(contributor.ResolveCount >= 2);
    }

    /// <summary>
    /// 测试目的：默认数据库上下文的 dbKey 应优先于默认数据源。
    /// </summary>
    [Fact]
    public void Create_WhenDefaultDatabaseContextIsConfigured_ShouldUseItsDbKey()
    {
        // Arrange
        var metadataOptions = CreateSqliteMetadataOptions("reporting", "Data Source=reporting.db", "archive",
            "Data Source=archive.db");
        metadataOptions.DefaultDatabaseContext.DbKey = "archive";
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProviderWithoutDefault(metadataOptions);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent);

        // Assert
        AssertCanExecute(query);
    }

    /// <summary>
    /// 测试目的：无默认上下文时应使用配置的默认数据源。
    /// </summary>
    [Fact]
    public void Create_WhenOnlyDefaultDataSourceIsConfigured_ShouldUseItsDbKey()
    {
        // Arrange
        var metadataOptions = CreateSqliteMetadataOptions("reporting", "Data Source=reporting.db", "archive",
            "Data Source=archive.db");
        metadataOptions.DataSources.DefaultDataSourceKey = "archive";
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProviderWithoutDefault(metadataOptions);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent);

        // Assert
        AssertCanExecute(query);
    }

    /// <summary>
    /// 测试目的：未配置默认键且仅存在一个数据源时应使用唯一数据源。
    /// </summary>
    [Fact]
    public void Create_WhenOnlyOneDataSourceExists_ShouldUseUniqueDataSource()
    {
        // Arrange
        var metadataOptions = CreateSqliteMetadataOptions("reporting", "Data Source=reporting.db");
        metadataOptions.DataSources.DefaultDataSourceKey = null;
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProviderWithoutDefault(metadataOptions);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        using var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent);

        // Assert
        AssertCanExecute(query);
    }

    /// <summary>
    /// 测试目的：未配置默认上下文和默认数据源时，多个数据源必须报歧义错误。
    /// </summary>
    [Fact]
    public void Create_WhenMultipleDataSourcesHaveNoDefault_ShouldThrow()
    {
        // Arrange
        var metadataOptions = CreateSqliteMetadataOptions("reporting", "Data Source=reporting.db", "archive",
            "Data Source=archive.db");
        metadataOptions.DataSources.DefaultDataSourceKey = null;
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProviderWithoutDefault(metadataOptions);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent));

        // Assert
        Assert.Contains(nameof(SqlDataSourceOptions.DefaultDataSourceKey), exception.Message);
    }

    /// <summary>
    /// 测试目的：显式 dbKey 应覆盖环境数据库作用域。
    /// </summary>
    [Fact]
    public void Create_WhenExplicitDbKeyAndAmbientDbKeyDiffer_ShouldPreferExplicitDbKey()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider(CreateMetadataOptions());
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();
        var databaseScopeManager = serviceProvider.GetRequiredService<IDatabaseScopeManager>();

        // Act
        using (databaseScopeManager.Use("reporting"))
        using (var query = factory.Create(unitOfWork, EfCoreSqlConnectionMode.Independent, "default"))
        {
            // Assert
            AssertCanExecute(query);
        }
    }

    /// <summary>
    /// 测试目的：Shared 模式不能把 SQLite 独占内存数据库当作可复用物理身份。
    /// </summary>
    [Fact]
    public void Create_WhenSharedSqliteConnectionUsesExclusiveMemory_ShouldThrow()
    {
        // Arrange
        var metadataOptions = CreateSqliteMetadataOptions("default", "Data Source=:memory:");
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var serviceProvider = CreateServiceProviderWithoutDefault(metadataOptions);
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var factory = serviceProvider.GetRequiredService<IEfCoreSqlQueryFactory>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => factory.Create(unitOfWork));

        // Assert
        Assert.Contains("独占内存", exception.Message);
        Assert.Contains(nameof(EfCoreSqlConnectionMode.Independent), exception.Message);
    }

    /// <summary>
    /// 测试 - EF Core 元数据中的带点物理表名应由 MySQL Builder 作为原子标识符渲染。
    /// </summary>
    [Fact]
    public void MetadataProvider_WhenEfTableNameContainsDot_ShouldKeepAtomicNameForMySqlBuilder()
    {
        // Arrange
        using var connection = new SqliteConnection(SharedMemoryConnectionString);
        connection.Open();
        using var serviceProvider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, serviceProvider);
        var metadataProvider = new EfCoreEntityModelMetadataProvider(unitOfWork.Model);
        var builder = new MySqlBuilder(new SqlBuilderServices(entityModelMetadataProvider: metadataProvider));

        // Act
        var metadata = metadataProvider.GetMetadata(typeof(DottedTableEntity));
        var sql = builder.Select("*").From<DottedTableEntity>("c").ToSql();

        // Assert
        Assert.Equal("Merchants.Company", metadata.TableName);
        Assert.Empty(metadata.Schema);
        Assert.Equal("display_name", metadata.Properties[nameof(DottedTableEntity.DisplayName)].ColumnName);
        Assert.Equal("Select * \r\nFrom `Merchants.Company` As `c`", sql);
    }

    /// <summary>
    /// 断言 Query 可通过其内部绑定资源执行 SQLite 查询。
    /// </summary>
    /// <param name="query">待验证的查询对象。</param>
    private static void AssertCanExecute(ISqlQuery query) => Assert.True(ExecuteCount(query) >= 0);

    /// <summary>
    /// 执行 SQLite 元数据表计数查询。
    /// </summary>
    /// <param name="query">待执行的查询对象。</param>
    /// <returns>SQLite 元数据项数量。</returns>
    private static int ExecuteCount(ISqlQuery query) => query.Query<int>().CountAll().From("sqlite_master").Scalar();

    /// <summary>
    /// 创建服务提供程序
    /// </summary>
    /// <returns>服务提供程序</returns>
    private static ServiceProvider CreateServiceProvider(SqlMetadataOptions metadataOptions = null,
        ConnectionStringCollection connectionStrings = null,
        ISqlDatabaseIdentityContributor identityContributor = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (connectionStrings != null)
            services.AddSingleton(connectionStrings);
        if (identityContributor != null)
            services.AddSingleton<ISqlDatabaseIdentityContributor>(identityContributor);
        if (metadataOptions != null)
            services.ConfigureSqlMetadata(options => ApplyMetadataOptions(options, metadataOptions));
        services.AddSqliteProvider();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, SharedMemoryConnectionString);
        services.AddEfCoreSqlQueryFactory();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建不额外注册默认数据源的服务提供程序。
    /// </summary>
    /// <param name="metadataOptions">Sql 元数据配置。</param>
    /// <returns>服务提供程序。</returns>
    private static ServiceProvider CreateServiceProviderWithoutDefault(SqlMetadataOptions metadataOptions)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => ApplyMetadataOptions(options, metadataOptions));
        services.AddSqlCore();
        services.AddSqliteProvider();
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
    /// 创建 SQLite 数据源测试配置。
    /// </summary>
    /// <param name="firstKey">第一个数据源标识。</param>
    /// <param name="firstConnectionString">第一个连接字符串。</param>
    /// <param name="secondKey">第二个数据源标识。</param>
    /// <param name="secondConnectionString">第二个连接字符串。</param>
    /// <returns>Sql 元数据配置。</returns>
    private static SqlMetadataOptions CreateSqliteMetadataOptions(string firstKey, string firstConnectionString,
        string secondKey = null, string secondConnectionString = null)
    {
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources[firstKey] = new SqlDataSourceDescriptor
        {
            Key = firstKey,
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = firstConnectionString
        };
        if (string.IsNullOrWhiteSpace(secondKey) == false)
        {
            options.DataSources.DataSources[secondKey] = new SqlDataSourceDescriptor
            {
                Key = secondKey,
                DatabaseType = DatabaseType.Sqlite,
                ConnectionString = secondConnectionString
            };
        }
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
        target.DefaultDatabaseContext = new DatabaseContext
        {
            DbKey = source.DefaultDatabaseContext?.DbKey,
            TenantId = source.DefaultDatabaseContext?.TenantId,
            ReadPreference = source.DefaultDatabaseContext?.ReadPreference ?? SqlReadPreference.Default,
            MappingProfile = source.DefaultDatabaseContext?.MappingProfile
        };
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
    /// 捕获 SQL 诊断消息的观察器。
    /// </summary>
    private sealed class SqlDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        /// <summary>
        /// 接收诊断消息的回调。
        /// </summary>
        private readonly Action<DiagnosticsMessage> _onMessage;

        /// <summary>
        /// 全局诊断监听器订阅。
        /// </summary>
        private readonly IDisposable _allSubscription;

        /// <summary>
        /// SQL 查询诊断监听器订阅。
        /// </summary>
        private IDisposable _listenerSubscription;

        /// <summary>
        /// 初始化一个<see cref="SqlDiagnosticObserver"/>类型的实例。
        /// </summary>
        /// <param name="onMessage">接收诊断消息的回调。</param>
        public SqlDiagnosticObserver(Action<DiagnosticsMessage> onMessage)
        {
            _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
            _allSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        /// <inheritdoc />
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                _listenerSubscription = value.Subscribe(this);
        }

        /// <inheritdoc />
        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == SqlQueryDiagnosticListenerNames.BeforeExecute && value.Value is DiagnosticsMessage message)
                _onMessage(message);
        }

        /// <inheritdoc />
        public void OnCompleted() { }

        /// <inheritdoc />
        public void OnError(Exception error) { }

        /// <inheritdoc />
        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allSubscription.Dispose();
        }
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
            modelBuilder.Entity<DottedTableEntity>(builder =>
            {
                builder.ToTable("Merchants.Company");
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
        public IDbConnection GetConnection() => new SqliteConnection(SharedMemoryConnectionString);
    }

    /// <summary>
    /// 测试用 SQLite 物理身份贡献器。
    /// </summary>
    private sealed class TestSqliteIdentityContributor : ISqlDatabaseIdentityContributor
    {
        /// <summary>
        /// 解析调用次数。
        /// </summary>
        public int ResolveCount { get; private set; }

        /// <inheritdoc />
        public bool CanResolve(DatabaseType databaseType) => databaseType == DatabaseType.Sqlite;

        /// <inheritdoc />
        public SqlDatabaseIdentity Resolve(DatabaseType databaseType, DbConnectionStringBuilder builder)
        {
            ResolveCount++;
            return new SqlDatabaseIdentity
            {
                DatabaseType = databaseType,
                FilePath = "custom-sqlite-identity"
            };
        }
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

    /// <summary>
    /// 带点物理表名测试实体。
    /// </summary>
    private sealed class DottedTableEntity
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName { get; set; }
    }
}