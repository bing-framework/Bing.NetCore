using System.Collections.Concurrent;
using Bing.Caching.ContractTests;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit.Sdk;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证共享契约断言可以识别受控的错误 Provider 行为。
/// </summary>
public class SharedContractAssertionsTest
{
    /// <summary>
    /// 同步或异步条件写入报告多个成功者时，共享原子性断言必须失败。
    /// </summary>
    /// <param name="brokenAsync">是否仅破坏异步条件写入。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicTrySet_RejectsMultipleWinners(bool brokenAsync)
    {
        var entries = new ConcurrentDictionary<string, int>();
        var cache = new Mock<ICache>();
        cache.Setup(x => x.TrySet(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CacheOptions>()))
            .Returns((string key, int value, CacheOptions _) => !brokenAsync || entries.TryAdd(key, value));
        cache.Setup(x => x.TrySetAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CacheOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        cache.Setup(x => x.GetAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => entries[key]);
        cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) =>
            {
                entries.TryRemove(key, out var ignored);
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<XunitException>(() => CacheContractAssertions.VerifyAtomicTrySetAsync(cache.Object));
        cache.Verify(x => x.TrySetAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CacheOptions>(),
                It.IsAny<CancellationToken>()), brokenAsync ? Times.AtLeastOnce() : Times.Never());
    }

    /// <summary>
    /// 移除和软过期无效时，共享失效断言必须失败。
    /// </summary>
    /// <param name="fault">要注入的错误。</param>
    [Theory]
    [InlineData(HybridFault.Remove)]
    [InlineData(HybridFault.Expire)]
    public async Task Invalidation_RejectsNoOpOperations(HybridFault fault)
    {
        using var services = TestCache.Create();
        var broken = new FaultyHybridCache(services.GetRequiredService<IHybridCache>(), fault);
        await Assert.ThrowsAnyAsync<XunitException>(() => HybridCacheContractAssertions.VerifyRemovalAndExpiryAsync(broken));
    }

    /// <summary>
    /// Provider 忽略显式 TTL 时，共享期限断言必须失败。
    /// </summary>
    [Fact]
    public async Task ExplicitTtl_RejectsIgnoredExpiration()
    {
        using var services = TestCache.Create();
        var broken = new FaultyHybridCache(services.GetRequiredService<IHybridCache>(), HybridFault.IgnoreTtl);
        await Assert.ThrowsAnyAsync<XunitException>(() => HybridCacheContractAssertions.VerifyExplicitTtlAsync(
            broken, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(700)));
    }

    /// <summary>
    /// 等待者提前成功而不是等待和响应取消时，可选策略断言必须失败。
    /// </summary>
    [Fact]
    public async Task WaiterCancellation_RejectsPrematureCompletion()
    {
        using var services = TestCache.Create();
        var broken = new FaultyHybridCache(services.GetRequiredService<IHybridCache>(), HybridFault.Waiter);
        await Assert.ThrowsAnyAsync<XunitException>(() => HybridCacheContractAssertions.VerifyWaiterCancellationAsync(broken));
    }

    /// <summary>
    /// 供错误注入测试选择的故障类型。
    /// </summary>
    public enum HybridFault { Remove, Expire, IgnoreTtl, Waiter }

    private sealed class FaultyHybridCache(IHybridCache inner, HybridFault fault) : IHybridCache
    {
        public string Name => inner.Name;

        public Task<CacheEntry<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            inner.TryGetAsync<T>(key, cancellationToken);

        public Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
            CacheOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) =>
            fault == HybridFault.Waiter && cancellationToken.CanBeCanceled
                ? Task.FromResult(default(T)!)
                : inner.GetOrCreateAsync(key, factory, options, tags, cancellationToken);

        public Task SetAsync<T>(string key, T value, CacheOptions? options = null,
            IEnumerable<string>? tags = null, CancellationToken cancellationToken = default) =>
            inner.SetAsync(key, value, fault == HybridFault.IgnoreTtl ? null : options, tags, cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            fault == HybridFault.Remove ? Task.CompletedTask : inner.RemoveAsync(key, cancellationToken);

        public Task ExpireAsync(string key, CancellationToken cancellationToken = default) =>
            fault == HybridFault.Expire ? Task.CompletedTask : inner.ExpireAsync(key, cancellationToken);

        public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default) =>
            inner.InvalidateTagAsync(tag, cancellationToken);

        public Task ClearAsync(CancellationToken cancellationToken = default) => inner.ClearAsync(cancellationToken);
    }
}
