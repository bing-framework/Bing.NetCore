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
    /// 测试目的：无跟踪单实体查询必须将调用方取消令牌传递给标识列表查询。
    /// </summary>
    [Fact]
    public async Task FindByIdNoTrackingAsync_WhenCalled_ShouldPassCancellationTokenToIdsQuery()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var store = new CancellationCapturingStore(unitOfWork);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await store.FindByIdNoTrackingAsync(Guid.NewGuid(), cancellationTokenSource.Token);

        // Assert
        Assert.Equal(cancellationTokenSource.Token, store.CapturedCancellationToken);
    }

    /// <summary>
    /// 测试目的：预取消查询必须先于标识对象字符串转换和字符串标识解析终止。
    /// </summary>
    [Fact]
    public async Task FindAsync_WhenCancellationRequested_ShouldNotConvertIdentifiers()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var store = new VersionedSampleStore(unitOfWork);
        var id = new SideEffectIdentifier();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
#pragma warning disable CS0618
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.FindAsync(id, cancellationTokenSource.Token));
#pragma warning restore CS0618
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.FindByIdsAsync("not-a-guid",
            cancellationTokenSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.FindByIdsNoTrackingAsync("not-a-guid",
            cancellationTokenSource.Token));

        // Assert
        Assert.Equal(0, id.ToStringCallCount);
    }

    /// <summary>
    /// 测试目的：预取消实体删除必须先于实体标识读取终止，不能触发自定义 Id getter。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WhenCancellationRequested_ShouldNotReadEntityId()
    {
        // Arrange
        using var connection = CreateOpenConnection();
        using var provider = CreateServiceProvider();
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var store = new SideEffectKeySampleStore(unitOfWork);
        var entity = new SideEffectKeySample();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RemoveAsync(entity,
            cancellationTokenSource.Token));

        // Assert
        Assert.Equal(0, entity.IdReadCount);
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
    /// 捕获标识列表查询取消令牌的测试存储器。
    /// </summary>
    private sealed class CancellationCapturingStore : StoreBase<VersionedSample>
    {
        /// <summary>
        /// 初始化一个 <see cref="CancellationCapturingStore"/> 类型的实例。
        /// </summary>
        /// <param name="unitOfWork">工作单元。</param>
        public CancellationCapturingStore(VersionedUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        /// <summary>
        /// 最近一次接收的取消令牌。
        /// </summary>
        public CancellationToken CapturedCancellationToken { get; private set; }

        /// <inheritdoc />
        public override Task<List<VersionedSample>> FindByIdsNoTrackingAsync(IEnumerable<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            CapturedCancellationToken = cancellationToken;
            return Task.FromResult(new List<VersionedSample>());
        }
    }

    /// <summary>
    /// 读取标识时记录访问次数的测试实体。
    /// </summary>
    private sealed class SideEffectKeySample : IKey<Guid>
    {
        private readonly Guid _id = Guid.NewGuid();

        /// <summary>
        /// 标识读取次数。
        /// </summary>
        public int IdReadCount { get; private set; }

        /// <inheritdoc />
        public Guid Id
        {
            get
            {
                IdReadCount++;
                return _id;
            }
        }
    }

    /// <summary>
    /// 用于验证实体标识访问顺序的存储器。
    /// </summary>
    private sealed class SideEffectKeySampleStore : StoreBase<SideEffectKeySample>
    {
        /// <summary>
        /// 初始化一个 <see cref="SideEffectKeySampleStore"/> 类型的实例。
        /// </summary>
        /// <param name="unitOfWork">工作单元。</param>
        public SideEffectKeySampleStore(VersionedUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }

    /// <summary>
    /// 字符串转换时记录访问次数的测试标识。
    /// </summary>
    private sealed class SideEffectIdentifier
    {
        /// <summary>
        /// 字符串转换次数。
        /// </summary>
        public int ToStringCallCount { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            ToStringCallCount++;
            return "identifier";
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