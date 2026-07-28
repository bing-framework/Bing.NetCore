using Bing.Data.Transaction;
using Bing.Datas.EntityFramework.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// <see cref="UnitOfWorkBase"/> 保存行为单元测试。
/// </summary>
public class UnitOfWorkSaveChangesTest
{
    /// <summary>
    /// 测试目的：同步保存成功后应发布一次领域事件。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenSuccessful_ShouldPublishEventsOnce()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Database.EnsureCreated();
        unitOfWork.Samples.Add(new SaveSample { Name = "sync" });

        // Act
        var result = unitOfWork.SaveChanges();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, unitOfWork.PublishEventsCount);
    }

    /// <summary>
    /// 测试目的：异步保存成功后应发布一次领域事件。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenSuccessful_ShouldPublishEventsOnce()
    {
        // Arrange
        await using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        unitOfWork.Samples.Add(new SaveSample { Name = "async" });

        // Act
        var result = await unitOfWork.SaveChangesAsync();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, unitOfWork.PublishEventsCount);
    }

    /// <summary>
    /// 测试目的：保存失败时不得发布领域事件。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenPersistenceFails_ShouldNotPublishEvents()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        unitOfWork.Samples.Add(new SaveSample { Name = "failure" });

        // Act and Assert
        Assert.Throws<DbUpdateException>(() => unitOfWork.SaveChanges());
        Assert.Equal(0, unitOfWork.PublishEventsCount);
    }

    /// <summary>
    /// 创建已打开的 SQLite 内存连接。
    /// </summary>
    /// <returns>SQLite 数据库连接。</returns>
    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// 创建测试服务提供程序。
    /// </summary>
    /// <returns>服务提供程序。</returns>
    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建测试工作单元。
    /// </summary>
    /// <param name="connection">SQLite 数据库连接。</param>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <returns>测试工作单元。</returns>
    private static EventPublishingUnitOfWork CreateUnitOfWork(SqliteConnection connection,
        IServiceProvider serviceProvider)
    {
        var options = new DbContextOptionsBuilder<EventPublishingUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new EventPublishingUnitOfWork(options, serviceProvider);
    }

    /// <summary>
    /// 可统计事件发布次数的测试工作单元。
    /// </summary>
    private sealed class EventPublishingUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个 <see cref="EventPublishingUnitOfWork"/> 类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public EventPublishingUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 测试实体集合。
        /// </summary>
        public DbSet<SaveSample> Samples { get; set; }

        /// <summary>
        /// 事件发布次数。
        /// </summary>
        public int PublishEventsCount { get; private set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SaveSample>(builder =>
            {
                builder.ToTable("save_samples");
                builder.HasKey(entity => entity.Id);
            });
        }

        /// <inheritdoc />
        protected override void SaveChangesBefore()
        {
        }

        /// <inheritdoc />
        protected override Task PublishEventsAsync()
        {
            PublishEventsCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 保存测试实体。
    /// </summary>
    private sealed class SaveSample
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