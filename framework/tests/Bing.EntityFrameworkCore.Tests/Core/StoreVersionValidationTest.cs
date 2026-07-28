using Bing.Data.Transaction;
using Bing.Datas.EntityFramework.Core;
using Bing.Domain.Entities;
using Bing.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// <see cref="StoreBase{TEntity, TKey}"/> 版本校验单元测试。
/// </summary>
public class StoreVersionValidationTest
{
    /// <summary>
    /// 测试目的：输入版本与原始版本完全一致时应允许更新。
    /// </summary>
    [Fact]
    public void Update_WhenVersionMatchesOriginal_ShouldNotThrow()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var entity = AttachWithOriginalVersion(unitOfWork, new byte[] { 1, 2, 3, 4 });
        entity.Version = new byte[] { 1, 2, 3, 4 };
        var store = new VersionedSampleStore(unitOfWork);

        // Act and Assert
        var exception = Record.Exception(() => store.Update(entity));
        Assert.Null(exception);
    }

    /// <summary>
    /// 测试目的：输入版本长度短于原始版本时应抛出并发异常，而不是访问数组越界。
    /// </summary>
    [Fact]
    public void Update_WhenVersionIsShorterThanOriginal_ShouldThrowConcurrencyException()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var entity = AttachWithOriginalVersion(unitOfWork, new byte[] { 1, 2, 3, 4 });
        entity.Version = new byte[] { 1, 2, 3 };
        var store = new VersionedSampleStore(unitOfWork);

        // Act and Assert
        Assert.Throws<ConcurrencyException>(() => store.Update(entity));
    }

    /// <summary>
    /// 测试目的：输入版本长度较长且额外字节不同时应抛出并发异常，不能忽略尾部差异。
    /// </summary>
    [Fact]
    public void Update_WhenVersionIsLongerWithDifferentSuffix_ShouldThrowConcurrencyException()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var entity = AttachWithOriginalVersion(unitOfWork, new byte[] { 1, 2, 3, 4 });
        entity.Version = new byte[] { 1, 2, 3, 4, 5 };
        var store = new VersionedSampleStore(unitOfWork);

        // Act and Assert
        Assert.Throws<ConcurrencyException>(() => store.Update(entity));
    }

    /// <summary>
    /// 附加带指定原始版本的实体。
    /// </summary>
    /// <param name="unitOfWork">测试工作单元。</param>
    /// <param name="version">原始版本。</param>
    /// <returns>已跟踪实体。</returns>
    private static VersionedSample AttachWithOriginalVersion(VersionedUnitOfWork unitOfWork, byte[] version)
    {
        var entity = new VersionedSample { Id = Guid.NewGuid(), Version = version };
        unitOfWork.Attach(entity);
        return entity;
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
    /// 创建版本测试工作单元。
    /// </summary>
    /// <param name="connection">SQLite 数据库连接。</param>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <returns>版本测试工作单元。</returns>
    private static VersionedUnitOfWork CreateUnitOfWork(SqliteConnection connection, IServiceProvider serviceProvider)
    {
        var options = new DbContextOptionsBuilder<VersionedUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new VersionedUnitOfWork(options, serviceProvider);
    }

    /// <summary>
    /// 版本测试工作单元。
    /// </summary>
    private sealed class VersionedUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个 <see cref="VersionedUnitOfWork"/> 类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public VersionedUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VersionedSample>(builder => builder.HasKey(entity => entity.Id));
        }
    }

    /// <summary>
    /// 版本测试存储器。
    /// </summary>
    private sealed class VersionedSampleStore : StoreBase<VersionedSample>
    {
        /// <summary>
        /// 初始化一个 <see cref="VersionedSampleStore"/> 类型的实例。
        /// </summary>
        /// <param name="unitOfWork">工作单元。</param>
        public VersionedSampleStore(VersionedUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }

    /// <summary>
    /// 具备乐观锁版本的测试实体。
    /// </summary>
    private sealed class VersionedSample : IKey<Guid>, IVersion
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 版本号。
        /// </summary>
        public byte[] Version { get; set; }
    }
}