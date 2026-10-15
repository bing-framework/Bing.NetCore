using Bing.Auditing.Persistence;
using Bing.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证审计保留清理后台调度的边界。
/// </summary>
public sealed class AuditLogRetentionHostedServiceTest
{
    /// <summary>
    /// 调度默认关闭时不应调用清理服务。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenDisabled_ShouldNotRun()
    {
        var service = new RecordingRetentionService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionService>(service);
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = false;
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(0, service.CallCount);
    }

    /// <summary>
    /// 启用调度并立即运行时应通过高层服务执行单个有界批次。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenEnabled_ShouldRunAuthorizedBatch()
    {
        var service = new RecordingRetentionService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionService>(service);
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.RetentionPeriod = TimeSpan.FromDays(7);
            options.BatchSize = 17;
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await service.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(1, service.CallCount);
        Assert.Equal(17, service.LastBatchSize);
        Assert.Equal(DateTimeKind.Utc, service.LastCutoffUtc.Kind);
        Assert.InRange(service.LastCutoffUtc, DateTime.UtcNow.AddDays(-7).AddMinutes(-1),
            DateTime.UtcNow.AddDays(-7).AddMinutes(1));
    }

    /// <summary>
    /// 停止时应等待不响应取消的当前存储调用完成。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCurrentBatchIgnoresCancellation_ShouldWaitForCompletion()
    {
        var service = new BlockingRetentionService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionService>(service);
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var stopCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var stopTask = hosted.StopAsync(stopCancellation.Token);
        await Task.Delay(100);
        Assert.False(stopTask.IsCompleted);

        service.Release.TrySetResult(true);
        await stopTask;
        Assert.Equal(1, service.CallCount);
    }

    /// <summary>
    /// 未获取到协调租约时应跳过本轮清理。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCoordinationLeaseUnavailable_ShouldSkipBatch()
    {
        var service = new RecordingRetentionService();
        var leaseProvider = new RecordingLeaseProvider();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionService>(service);
        services.AddSingleton<ILockLeaseProvider>(leaseProvider);
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.UseCoordinationLease = true;
            options.CoordinationWaitTimeout = TimeSpan.Zero;
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(1, leaseProvider.CallCount);
        Assert.Equal(0, service.CallCount);
    }

    /// <summary>
    /// 成功获取协调租约时应执行清理并只释放一次租约。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCoordinationLeaseAcquired_ShouldReleaseLeaseOnce()
    {
        var service = new RecordingRetentionService();
        var lease = new RecordingLease("audit-retention");
        var leaseProvider = new RecordingLeaseProvider(lease);
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionService>(service);
        services.AddSingleton<ILockLeaseProvider>(leaseProvider);
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.UseCoordinationLease = true;
            options.CoordinationKey = "audit-retention";
            options.CoordinationWaitTimeout = TimeSpan.Zero;
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await service.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(1, leaseProvider.CallCount);
        Assert.Equal("audit-retention", leaseProvider.LastKey);
        Assert.Equal(1, service.CallCount);
        Assert.Equal(1, lease.ReleaseCount);
    }

    /// <summary>
    /// 启用协调租约却未注册提供程序时应在启动阶段拒绝配置。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCoordinationProviderMissing_ShouldRejectStartup()
    {
        var service = new RecordingRetentionService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionService>(service);
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.UseCoordinationLease = true;
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hosted.StartAsync(CancellationToken.None));
        Assert.Equal(0, service.CallCount);
    }

    /// <summary>
    /// 启用调度却未注册清理服务时应在启动阶段拒绝配置。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenRetentionServiceMissing_ShouldRejectStartup()
    {
        var services = new ServiceCollection();
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
        });
        services.RemoveAll<IAuditLogRetentionService>();

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hosted.StartAsync(CancellationToken.None));
        Assert.Contains(nameof(IAuditLogRetentionService), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 协调租约提供程序为作用域服务时，托管服务不应捕获根作用域。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCoordinationProviderIsScoped_ShouldBuildWithValidateScopes()
    {
        var service = new RecordingRetentionService();
        var leaseProvider = new RecordingLeaseProvider();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogRetentionService>(service);
        services.AddScoped<ILockLeaseProvider>(_ => leaseProvider);
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.UseCoordinationLease = true;
        });

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is AuditLogRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(1, leaseProvider.CallCount);
        Assert.Equal(0, service.CallCount);
    }

    /// <summary>
    /// 启用调度时应拒绝无效间隔，而不是启动无界紧循环。
    /// </summary>
    [Fact]
    public void Scheduler_WhenIntervalIsInvalid_ShouldRejectRegistration()
    {
        var services = new ServiceCollection();
        services.AddBingAuditLogRetentionScheduler<AllowingAuthorizer>(options =>
        {
            options.Enabled = true;
            options.Interval = TimeSpan.Zero;
        });

        using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            provider.GetServices<IHostedService>().Single(item => item is AuditLogRetentionHostedService));
    }

    /// <summary>
    /// 记录高层保留服务调用的测试实现。
    /// </summary>
    private sealed class RecordingRetentionService : IAuditLogRetentionService
    {
        /// <summary>
        /// 标识首次调用。
        /// </summary>
        internal readonly TaskCompletionSource<bool> FirstCall =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取调用次数。
        /// </summary>
        internal int CallCount { get; private set; }

        /// <summary>
        /// 获取最近一次截止时间。
        /// </summary>
        internal DateTime LastCutoffUtc { get; private set; }

        /// <summary>
        /// 获取最近一次批量大小。
        /// </summary>
        internal int LastBatchSize { get; private set; }

        /// <inheritdoc />
        public Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastCutoffUtc = cutoffUtc;
            LastBatchSize = batchSize;
            FirstCall.TrySetResult(true);
            return Task.FromResult(batchSize);
        }
    }

    /// <summary>
    /// 忽略取消并等待外部释放的测试保留服务。
    /// </summary>
    private sealed class BlockingRetentionService : IAuditLogRetentionService
    {
        /// <summary>
        /// 标识清理调用已经开始。
        /// </summary>
        internal readonly TaskCompletionSource<bool> Started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 允许清理调用结束。
        /// </summary>
        internal readonly TaskCompletionSource<bool> Release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取调用次数。
        /// </summary>
        internal int CallCount { get; private set; }

        /// <inheritdoc />
        public async Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Started.TrySetResult(true);
            await Release.Task.ConfigureAwait(false);
            return batchSize;
        }
    }

    /// <summary>
    /// 允许后台清理的测试授权器。
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

    /// <summary>
    /// 记录协调租约请求并返回预设租约的测试实现。
    /// </summary>
    private sealed class RecordingLeaseProvider : ILockLeaseProvider
    {
        /// <summary>
        /// 初始化一个 <see cref="RecordingLeaseProvider" /> 类型的实例。
        /// </summary>
        public RecordingLeaseProvider()
        {
        }

        /// <summary>
        /// 初始化一个 <see cref="RecordingLeaseProvider" /> 类型的实例。
        /// </summary>
        /// <param name="lease">要返回的租约。</param>
        public RecordingLeaseProvider(ILockLease lease)
        {
            Lease = lease;
        }

        /// <summary>
        /// 获取调用次数。
        /// </summary>
        internal int CallCount { get; private set; }

        /// <summary>
        /// 获取最近一次锁键。
        /// </summary>
        internal string LastKey { get; private set; }

        /// <summary>
        /// 获取预设租约。
        /// </summary>
        internal ILockLease Lease { get; }

        /// <inheritdoc />
        public Task<ILockLease> TryAcquireLeaseAsync(string key, TimeSpan waitTimeout,
            TimeSpan? leaseDuration = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastKey = key;
            return Task.FromResult(Lease);
        }
    }

    /// <summary>
    /// 记录异步释放次数的测试租约。
    /// </summary>
    private sealed class RecordingLease : ILockLease
    {
        /// <summary>
        /// 初始化一个 <see cref="RecordingLease" /> 类型的实例。
        /// </summary>
        /// <param name="key">锁键。</param>
        public RecordingLease(string key)
        {
            Key = key;
            Owner = "test-owner";
        }

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public string Owner { get; }

        /// <summary>
        /// 获取释放次数。
        /// </summary>
        internal int ReleaseCount { get; private set; }

        /// <inheritdoc />
        public bool Release()
        {
            ReleaseCount++;
            return true;
        }

        /// <inheritdoc />
        public Task<bool> ReleaseAsync()
        {
            ReleaseCount++;
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public void Dispose() => Release();

        /// <inheritdoc />
        public ValueTask DisposeAsync() => new ValueTask(ReleaseAsync());
    }
}
