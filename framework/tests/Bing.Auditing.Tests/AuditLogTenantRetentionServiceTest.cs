using Bing.Auditing.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证租户范围审计保留清理入口。
/// </summary>
public sealed class AuditLogTenantRetentionServiceTest
{
    /// <summary>
    /// 授权完成后应把同一租户传递给低层存储。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_ShouldAuthorizeTenantBeforeStore()
    {
        var calls = new List<string>();
        var service = new AuditLogTenantRetentionService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        var deleted = await service.DeleteBeforeAsync("tenant-a", cutoff, 10);

        Assert.Equal(3, deleted);
        Assert.Equal(new[] { "authorize:tenant-a", "delete:tenant-a" }, calls);
    }

    /// <summary>
    /// 空白租户标识应在授权前拒绝。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenTenantIsMissing_ShouldRejectBeforeDependencies()
    {
        var calls = new List<string>();
        var service = new AuditLogTenantRetentionService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteBeforeAsync(" ", cutoff, 10));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 初始取消时不应调用租户授权器或存储。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenCancelledBeforeAuthorization_ShouldNotCallDependencies()
    {
        var calls = new List<string>();
        var service = new AuditLogTenantRetentionService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DeleteBeforeAsync("tenant-a", cutoff, 10, cancellation.Token));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 授权拒绝时不应调用低层存储。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotCallStore()
    {
        var calls = new List<string>();
        var service = new AuditLogTenantRetentionService(
            new RecordingStore(calls), new DenyingAuthorizer());
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteBeforeAsync("tenant-a", cutoff, 10));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 不能无损编码的租户标识应在授权前拒绝。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenTenantHasUnpairedSurrogate_ShouldRejectBeforeAuthorization()
    {
        var calls = new List<string>();
        var service = new AuditLogTenantRetentionService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        var cutoff = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<System.Text.EncoderFallbackException>(() =>
            service.DeleteBeforeAsync("tenant-\ud800", cutoff, 10));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 租户授权服务注册应幂等且尊重调用方预先注册的授权器。
    /// </summary>
    [Fact]
    public void AddBingAuditLogTenantRetention_ShouldKeepExistingAuthorizer()
    {
        var existing = new RecordingAuthorizer(new List<string>());
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogTenantRetentionAuthorizer>(existing);
        services.AddSingleton<IAuditLogTenantRetentionStore>(new RecordingStore(new List<string>()));
        services.AddBingAuditLogTenantRetention<DenyingAuthorizer>();
        services.AddBingAuditLogTenantRetention<AllowingAuthorizer>();

        using var provider = services.BuildServiceProvider();

        Assert.Same(existing, provider.GetRequiredService<IAuditLogTenantRetentionAuthorizer>());
        Assert.IsType<AuditLogTenantRetentionService>(provider.GetRequiredService<IAuditLogTenantRetentionService>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAuditLogTenantRetentionService));
    }

    /// <summary>
    /// 记录租户保留清理调用顺序的测试存储。
    /// </summary>
    private sealed class RecordingStore : IAuditLogTenantRetentionStore
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

        /// <inheritdoc />
        public Task<int> DeleteBeforeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add("delete:" + tenantId);
            return Task.FromResult(3);
        }
    }

    /// <summary>
    /// 记录租户授权调用的测试授权器。
    /// </summary>
    private sealed class RecordingAuthorizer : IAuditLogTenantRetentionAuthorizer
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
        public Task AuthorizeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add("authorize:" + tenantId);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 始终拒绝租户清理的测试授权器。
    /// </summary>
    private sealed class DenyingAuthorizer : IAuditLogTenantRetentionAuthorizer
    {
        /// <summary>
        /// 初始化一个 <see cref="DenyingAuthorizer" /> 类型的实例。
        /// </summary>
        public DenyingAuthorizer()
        {
        }

        /// <inheritdoc />
        public Task AuthorizeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new UnauthorizedAccessException("denied"));
    }

    /// <summary>
    /// 始终允许租户清理的测试授权器。
    /// </summary>
    private sealed class AllowingAuthorizer : IAuditLogTenantRetentionAuthorizer
    {
        /// <summary>
        /// 初始化一个 <see cref="AllowingAuthorizer" /> 类型的实例。
        /// </summary>
        public AllowingAuthorizer()
        {
        }

        /// <inheritdoc />
        public Task AuthorizeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
