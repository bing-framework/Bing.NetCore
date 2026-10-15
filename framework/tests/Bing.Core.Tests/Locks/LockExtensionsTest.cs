using Bing.Locks;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Locks;

/// <summary>
/// 验证锁便捷扩展的持有者语义。
/// </summary>
public sealed class LockExtensionsTest
{
    /// <summary>
    /// 自动释放入口为每次调用生成不同的持有者值。
    /// </summary>
    [Fact]
    public void ExecuteWithLock_ShouldUseUniqueOwner()
    {
        var @lock = new RecordingLock();

        @lock.ExecuteWithLock("key", TimeSpan.Zero, () => { });
        @lock.ExecuteWithLock("key", TimeSpan.Zero, () => { });

        Assert.Equal(2, @lock.Owners.Count);
        Assert.NotEqual(@lock.Owners[0], @lock.Owners[1]);
        Assert.All(@lock.Owners, owner => Assert.DoesNotContain(owner, new[] { "1" }));
    }

    /// <summary>
    /// 显式获取和释放便捷入口使用同一份唯一持有者。
    /// </summary>
    [Fact]
    public void LockTakAndRelease_ShouldUseTheSameUniqueOwner()
    {
        var @lock = new RecordingLock();

        Assert.True(@lock.LockTak("key", TimeSpan.Zero));
        Assert.True(@lock.LockRelease("key"));
        Assert.Equal(2, @lock.Owners.Count);
        Assert.Equal(@lock.Owners[0], @lock.Owners[1]);
        Assert.NotEqual("1", @lock.Owners[0]);
    }

    /// <summary>
    /// 旧持有者重复释放时不能释放已经接替的后继持有者。
    /// </summary>
    [Fact]
    public async Task LockTakAndRelease_WhenOwnerGenerationChanges_ShouldNotReleaseSuccessor()
    {
        var @lock = new LocalLock();
        var key = $"lock-extension-aba-{Guid.NewGuid():N}";
        // 让两个子任务继承同一个父执行上下文，覆盖 AsyncLocal 记录被并发共享的路径。
        @lock.LockTak(key, TimeSpan.Zero).ShouldBeTrue();
        @lock.LockRelease(key).ShouldBeTrue();
        var firstReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReleased = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var successorReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSuccessorRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var duplicateReleaseCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            try
            {
                (await @lock.LockTakAsync(key, TimeSpan.FromSeconds(2))).ShouldBeTrue();
                firstReady.SetResult(true);
                await allowFirstRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
                @lock.LockRelease(key).ShouldBeTrue();
                firstReleased.SetResult(true);
                await allowSuccessorRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var released = @lock.LockRelease(key);
                duplicateReleaseCompleted.SetResult(true);
                return released;
            }
            catch (Exception error)
            {
                firstReady.TrySetException(error);
                firstReleased.TrySetException(error);
                duplicateReleaseCompleted.TrySetException(error);
                throw;
            }
        });
        await firstReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var successor = Task.Run(async () =>
        {
            await firstReleased.Task.WaitAsync(TimeSpan.FromSeconds(5));
            (await @lock.LockTakAsync(key, TimeSpan.FromSeconds(2))).ShouldBeTrue();
            successorReady.SetResult(true);
            await duplicateReleaseCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return @lock.LockRelease(key);
        });

        allowFirstRelease.SetResult(true);
        await successorReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        allowSuccessorRelease.SetResult(true);

        (await first).ShouldBeFalse();
        (await successor).ShouldBeTrue();
    }

    /// <summary>
    /// 等待中的本地租约取消后应释放等待引用并允许后续获取。
    /// </summary>
    [Fact]
    public async Task TryAcquireLeaseAsync_WhenWaitingIsCancelled_ShouldReclaimWaiter()
    {
        var @lock = new LocalLock();
        var key = $"lock-extension-cancel-{Guid.NewGuid():N}";
        @lock.LockTake(key, "holder", TimeSpan.Zero).ShouldBeTrue();
        using var cancellation = new CancellationTokenSource();
        var waiting = @lock.TryAcquireLeaseAsync(key, TimeSpan.FromMinutes(1), cancellation.Token);
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => waiting);
        @lock.LockRelease(key, "holder").ShouldBeTrue();
        var next = await @lock.TryAcquireLeaseAsync(key, TimeSpan.Zero);
        next.ShouldNotBeNull();
        await next.DisposeAsync();
    }

    /// <summary>
    /// 通过 ILock 获取租约时应使用独立的租约提供契约。
    /// </summary>
    [Fact]
    public async Task TryAcquireLeaseAsync_WhenLockIsResolvedAsInterface_ShouldUseProvider()
    {
        ILock @lock = new LocalLock();
        var lease = await @lock.TryAcquireLeaseAsync($"lock-extension-provider-{Guid.NewGuid():N}", TimeSpan.Zero);
        lease.ShouldNotBeNull();
        (await lease.ReleaseAsync()).ShouldBeTrue();
    }

    /// <summary>
    /// 自定义锁未实现租约时不应因注册扩展而在解析其他服务时失败。
    /// </summary>
    [Fact]
    public void AddLocalLock_CustomImplementationWithoutLease_ShouldKeepLeaseOptional()
    {
        var services = new ServiceCollection();
        services.AddLocalLock<CustomLock>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILock>().ShouldBeOfType<CustomLock>();
        provider.GetService<ILockLeaseProvider>().ShouldBeNull();
    }

    /// <summary>
    /// 已注册的不支持租约的锁不应被默认扩展强制转换。
    /// </summary>
    [Fact]
    public void AddLocalLock_WhenCustomLockIsRegisteredFirst_ShouldKeepLeaseOptional()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILock, CustomLock>();
        services.AddLocalLock();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILock>().ShouldBeOfType<CustomLock>();
        provider.GetService<ILockLeaseProvider>().ShouldBeNull();
    }

    /// <summary>
    /// 记录锁扩展传入的持有者值。
    /// </summary>
    private sealed class RecordingLock : ILock
    {
        /// <summary>
        /// 获取持有者值。
        /// </summary>
        internal List<string> Owners { get; } = new();

        /// <inheritdoc />
        public bool LockTake(string key, string value, TimeSpan expiration)
        {
            Owners.Add(value);
            return true;
        }

        /// <inheritdoc />
        public Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration) =>
            Task.FromResult(LockTake(key, value, expiration));

        /// <inheritdoc />
        public bool LockRelease(string key, string value)
        {
            Owners.Add(value);
            return true;
        }

        /// <inheritdoc />
        public Task<bool> LockReleaseAsync(string key, string value) =>
            Task.FromResult(LockRelease(key, value));

        /// <inheritdoc />
        public void ExecuteWithLock(string key, string value, TimeSpan expiration, Action executeAction) =>
            Owners.Add(value);

        /// <inheritdoc />
        public Task ExecuteWithLockAsync(string key, string value, TimeSpan expiration, Func<Task> executeAction)
        {
            Owners.Add(value);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public T ExecuteWithLock<T>(string key, string value, TimeSpan expiration, Func<T> executeAction,
            T defaultValue = default)
        {
            Owners.Add(value);
            return defaultValue;
        }

        /// <inheritdoc />
        public Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction,
            T defaultValue = default)
        {
            Owners.Add(value);
            return Task.FromResult(defaultValue);
        }
    }

    /// <summary>
    /// 不提供租约能力的自定义锁夹具。
    /// </summary>
    public sealed class CustomLock : ILock
    {
        /// <inheritdoc />
        public bool LockTake(string key, string value, TimeSpan expiration) => true;

        /// <inheritdoc />
        public Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration) => Task.FromResult(true);

        /// <inheritdoc />
        public bool LockRelease(string key, string value) => true;

        /// <inheritdoc />
        public Task<bool> LockReleaseAsync(string key, string value) => Task.FromResult(true);

        /// <inheritdoc />
        public void ExecuteWithLock(string key, string value, TimeSpan expiration, Action executeAction) => executeAction?.Invoke();

        /// <inheritdoc />
        public Task ExecuteWithLockAsync(string key, string value, TimeSpan expiration, Func<Task> executeAction) =>
            executeAction?.Invoke() ?? Task.CompletedTask;

        /// <inheritdoc />
        public T ExecuteWithLock<T>(string key, string value, TimeSpan expiration, Func<T> executeAction, T defaultValue = default) =>
            executeAction == null ? defaultValue : executeAction();

        /// <inheritdoc />
        public Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction,
            T defaultValue = default) => executeAction == null ? Task.FromResult(defaultValue) : executeAction();
    }
}
