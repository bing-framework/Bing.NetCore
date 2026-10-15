using Bing.Auditing.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证经过授权的审计保留清理入口。
/// </summary>
public sealed class AuditLogRetentionServiceTest
{
    /// <summary>
    /// 授权完成后才应调用低层存储。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_ShouldAuthorizeBeforeStore()
    {
        var calls = new List<string>();
        var service = new AuditLogRetentionService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        var deleted = await service.DeleteBeforeAsync(cutoff, 10);

        Assert.Equal(3, deleted);
        Assert.Equal(new[] { "authorize", "delete" }, calls);
    }

    /// <summary>
    /// 授权拒绝时不应触发删除。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotDelete()
    {
        var store = new RecordingStore(new List<string>());
        var service = new AuditLogRetentionService(store, new DenyingAuthorizer());
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteBeforeAsync(cutoff, 10));

        Assert.Equal(0, store.DeleteCount);
    }

    /// <summary>
    /// 初始取消时不应调用授权器或存储。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenCancelledBeforeAuthorization_ShouldNotCallDependencies()
    {
        var calls = new List<string>();
        var service = new AuditLogRetentionService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DeleteBeforeAsync(cutoff, 10, cancellation.Token));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 超过框架批量上限时应在授权前拒绝请求。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenBatchExceedsLimit_ShouldRejectBeforeAuthorization()
    {
        var calls = new List<string>();
        var service = new AuditLogRetentionService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DeleteBeforeAsync(cutoff, 10001));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 授权服务注册应幂等且尊重调用方预先注册的授权器。
    /// </summary>
    [Fact]
    public void AddBingAuditLogRetention_ShouldKeepExistingAuthorizer()
    {
        var existing = new RecordingAuthorizer(new List<string>());
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionAuthorizer>(existing);
        services.AddSingleton<IAuditLogRetentionStore>(new RecordingStore(new List<string>()));
        services.AddBingAuditLogRetention<DenyingAuthorizer>();
        services.AddBingAuditLogRetention<AllowingAuthorizer>();

        using var provider = services.BuildServiceProvider();

        Assert.Same(existing, provider.GetRequiredService<IAuditLogRetentionAuthorizer>());
        Assert.IsType<AuditLogRetentionService>(provider.GetRequiredService<IAuditLogRetentionService>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAuditLogRetentionService));
    }

    /// <summary>
    /// 记录保留清理调用顺序的测试存储。
    /// </summary>
    private sealed class RecordingStore : IAuditLogRetentionStore
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
        /// 获取删除调用次数。
        /// </summary>
        public int DeleteCount { get; private set; }

        /// <inheritdoc />
        public Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add("delete");
            DeleteCount++;
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
