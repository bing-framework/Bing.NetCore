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
    /// 创建服务提供程序
    /// </summary>
    /// <returns>服务提供程序</returns>
    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabase<Bing.Data.IDatabase, TestDatabase>();
        services.AddSqliteSqlQuery("Data Source=:memory:");
        services.AddEfCoreSqlQueryFactory();
        return services.BuildServiceProvider();
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