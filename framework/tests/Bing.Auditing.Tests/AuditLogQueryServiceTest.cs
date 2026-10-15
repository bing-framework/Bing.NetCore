using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.Auditing.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证经过授权的审计查询入口。
/// </summary>
public sealed class AuditLogQueryServiceTest
{
    /// <summary>
    /// 授权完成后才应调用低层查询存储。
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldAuthorizeBeforeStore()
    {
        var calls = new List<string>();
        var service = new AuditLogQueryService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));
        var query = new AuditLogQuery { TenantId = "tenant-a" };

        var entries = await service.QueryAsync(query);

        Assert.Empty(entries);
        Assert.Equal(new[] { "authorize", "query" }, calls);
    }

    /// <summary>
    /// 授权拒绝时不应触发查询。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenAuthorizationFails_ShouldNotQuery()
    {
        var store = new RecordingStore(new List<string>());
        var service = new AuditLogQueryService(store, new DenyingAuthorizer());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.QueryAsync(
            new AuditLogQuery { TenantId = "tenant-a" }));

        Assert.Equal(0, store.QueryCount);
    }

    /// <summary>
    /// 空查询、缺少租户和预取消应在授权前失败。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenInvalidOrCancelled_ShouldNotCallDependencies()
    {
        var calls = new List<string>();
        var service = new AuditLogQueryService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.QueryAsync(null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(new AuditLogQuery()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync(
            new AuditLogQuery { TenantId = "tenant-a" }, cancellation.Token));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 高层查询服务拒绝越界分页和非法时间范围，不调用授权器或存储。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenQueryBoundsAreInvalid_ShouldNotCallDependencies()
    {
        var calls = new List<string>();
        var service = new AuditLogQueryService(
            new RecordingStore(calls), new RecordingAuthorizer(calls));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(
            new AuditLogQuery { TenantId = "tenant-a", Limit = 1001 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(
            new AuditLogQuery { TenantId = "tenant-a", Offset = 1000001 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(
            new AuditLogQuery { TenantId = "tenant-a", ApplicationName = new string('x', 257) }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(
            new AuditLogQuery
            {
                TenantId = "tenant-a",
                FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(
            new AuditLogQuery { TenantId = "   " }));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 查询服务注册应幂等且尊重调用方预先注册的授权器。
    /// </summary>
    [Fact]
    public void AddBingAuditLogQuery_ShouldKeepExistingAuthorizer()
    {
        var existing = new RecordingAuthorizer(new List<string>());
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogQueryAuthorizer>(existing);
        services.AddSingleton<IAuditLogQueryStore>(new RecordingStore(new List<string>()));
        services.AddBingAuditLogQuery<DenyingAuthorizer>();
        services.AddBingAuditLogQuery<AllowingAuthorizer>();

        using var provider = services.BuildServiceProvider();

        Assert.Same(existing, provider.GetRequiredService<IAuditLogQueryAuthorizer>());
        Assert.IsType<AuditLogQueryService>(provider.GetRequiredService<IAuditLogQueryService>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAuditLogQueryService));
    }

    /// <summary>
    /// 记录查询调用顺序的测试存储。
    /// </summary>
    private sealed class RecordingStore : IAuditLogQueryStore
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
        /// 获取查询调用次数。
        /// </summary>
        public int QueryCount { get; private set; }

        /// <inheritdoc />
        public Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add("query");
            QueryCount++;
            return Task.FromResult<IReadOnlyList<AuditLogEntry>>(Array.Empty<AuditLogEntry>());
        }
    }

    /// <summary>
    /// 记录授权调用的测试授权器。
    /// </summary>
    private sealed class RecordingAuthorizer : IAuditLogQueryAuthorizer
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
        public Task AuthorizeAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _calls.Add("authorize");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 始终拒绝操作的测试授权器。
    /// </summary>
    private sealed class DenyingAuthorizer : IAuditLogQueryAuthorizer
    {
        /// <summary>
        /// 初始化一个 <see cref="DenyingAuthorizer" /> 类型的实例。
        /// </summary>
        public DenyingAuthorizer()
        {
        }

        /// <inheritdoc />
        public Task AuthorizeAsync(AuditLogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromException(new UnauthorizedAccessException("denied"));
    }

    /// <summary>
    /// 始终允许操作的测试授权器。
    /// </summary>
    private sealed class AllowingAuthorizer : IAuditLogQueryAuthorizer
    {
        /// <summary>
        /// 初始化一个 <see cref="AllowingAuthorizer" /> 类型的实例。
        /// </summary>
        public AllowingAuthorizer()
        {
        }

        /// <inheritdoc />
        public Task AuthorizeAsync(AuditLogQuery query, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
