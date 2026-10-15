using Bing.Auditing.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证经过授权的审计归档入口。
/// </summary>
public sealed class AuditLogArchiveServiceTest
{
    /// <summary>
    /// 授权完成后才应调用低层归档存储。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_ShouldAuthorizeBeforeStore()
    {
        var calls = new List<string>();
        var service = new AuditLogArchiveService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        var archived = await service.ArchiveBeforeAsync(cutoff, 10);

        Assert.Equal(3, archived);
        Assert.Equal(new[] { "authorize", "archive" }, calls);
    }

    /// <summary>
    /// 授权拒绝时不应触发归档。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenAuthorizationFails_ShouldNotArchive()
    {
        var store = new RecordingStore(new List<string>());
        var service = new AuditLogArchiveService(store, new DenyingAuthorizer());
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ArchiveBeforeAsync(cutoff, 10));

        Assert.Equal(0, store.ArchiveCount);
    }

    /// <summary>
    /// 初始取消时不应调用授权器或存储。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenCancelledBeforeAuthorization_ShouldNotCallDependencies()
    {
        var calls = new List<string>();
        var service = new AuditLogArchiveService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ArchiveBeforeAsync(cutoff, 10, cancellation.Token));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 归档服务注册应幂等且尊重调用方预先注册的授权器。
    /// </summary>
    [Fact]
    public void AddBingAuditLogArchive_ShouldKeepExistingAuthorizer()
    {
        var existing = new RecordingAuthorizer(new List<string>());
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionAuthorizer>(existing);
        services.AddSingleton<IAuditLogArchiveStore>(new RecordingStore(new List<string>()));
        services.AddBingAuditLogArchive<DenyingAuthorizer>();
        services.AddBingAuditLogArchive<AllowingAuthorizer>();

        using var provider = services.BuildServiceProvider();

        Assert.Same(existing, provider.GetRequiredService<IAuditLogRetentionAuthorizer>());
        Assert.IsType<AuditLogArchiveService>(provider.GetRequiredService<IAuditLogArchiveService>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAuditLogArchiveService));
    }

    /// <summary>
    /// 记录归档调用顺序的测试存储。
    /// </summary>
    private sealed class RecordingStore : IAuditLogArchiveStore
    {
        /// <summary>
        /// 记录测试存储的调用顺序。
        /// </summary>
        private readonly IList<string> _calls;

        /// <summary>
        /// 初始化一个 <see cref="RecordingStore" /> 类型的实例。
        /// </summary>
        /// <param name="calls">调用记录集合。</param>
        public RecordingStore(IList<string> calls) => _calls = calls;

        /// <summary>
        /// 获取归档调用次数。
        /// </summary>
        public int ArchiveCount { get; private set; }

        /// <inheritdoc />
        public Task<int> ArchiveBeforeAsync(DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add("archive");
            ArchiveCount++;
            return Task.FromResult(3);
        }
    }

    /// <summary>
    /// 记录授权调用的测试授权器。
    /// </summary>
    private sealed class RecordingAuthorizer : IAuditLogRetentionAuthorizer
    {
        /// <summary>
        /// 记录测试授权器的调用顺序。
        /// </summary>
        private readonly IList<string> _calls;

        /// <summary>
        /// 初始化一个 <see cref="RecordingAuthorizer" /> 类型的实例。
        /// </summary>
        /// <param name="calls">调用记录集合。</param>
        public RecordingAuthorizer(IList<string> calls) => _calls = calls;

        /// <inheritdoc />
        public Task AuthorizeAsync(DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add("authorize");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 始终拒绝操作的测试授权器。
    /// </summary>
    private sealed class DenyingAuthorizer : IAuditLogRetentionAuthorizer
    {
        /// <summary>
        /// 初始化一个 <see cref="DenyingAuthorizer" /> 类型的实例。
        /// </summary>
        public DenyingAuthorizer()
        {
        }

        /// <inheritdoc />
        public Task AuthorizeAsync(DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new UnauthorizedAccessException("denied"));
    }

    /// <summary>
    /// 始终允许操作的测试授权器。
    /// </summary>
    private sealed class AllowingAuthorizer : IAuditLogRetentionAuthorizer
    {
        /// <summary>
        /// 初始化一个 <see cref="AllowingAuthorizer" /> 类型的实例。
        /// </summary>
        public AllowingAuthorizer()
        {
        }

        /// <inheritdoc />
        public Task AuthorizeAsync(DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
