using Bing.Caching.ContractTests;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 在 FusionCache Provider 上运行后端无关的 IHybridCache 契约断言。
/// </summary>
public class HybridCacheSharedContractTest
{
    /// <summary>
    /// 验证 FusionCache 适配器符合 Bing 混合缓存公共契约。
    /// </summary>
    [Fact]
    public async Task Provider_SatisfiesSharedHybridContract()
    {
        using var services = TestCache.Create();
        await HybridCacheContractAssertions.VerifyAsync(services.GetRequiredService<IHybridCache>());
    }

    /// <summary>
    /// 验证 FusionCache 声明的等待者独立取消策略。
    /// </summary>
    [Fact]
    public async Task Provider_SupportsIndependentWaiterCancellation()
    {
        using var services = TestCache.Create();
        await HybridCacheContractAssertions.VerifyWaiterCancellationAsync(services.GetRequiredService<IHybridCache>());
    }
}
