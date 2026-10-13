using Bing.Caching.ContractTests;
using FreeRedis;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 验证 FreeRedis 回源协调与资源限制契约。
/// </summary>
public class CoordinationBoundaryTest
{
    /// <summary>
    /// 用于运行协调边界测试的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;
    /// <summary>
    /// 初始化一个 <see cref="CoordinationBoundaryTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">Redis 测试客户端。</param>
    public CoordinationBoundaryTest(RedisClient client) => _client = client;

    /// <summary>
    /// 同键回源合并且失去租约后取消回源工厂。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public Task SameKeyMergeWithZeroQueue_AndLeaseLossCancelsFactory() =>
        CacheCoordinationAssertions.VerifySameKeyMergeAndLeaseLossAsync(new FreeRedisCacheManager(_client,
            new CacheConfiguration("merge-free-" + Guid.NewGuid().ToString("N"), "free",
                resources: new CacheResourceOptions(1, 0),
                coordination: new CacheCoordinationOptions(renewalInterval: TimeSpan.FromMilliseconds(50)))));

    /// <summary>
    /// 不同键可独立回源，批量载荷遵守资源上限。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public Task DifferentKeyFactoriesAndBatchPayload_RespectConfiguredResourceLimits() =>
        CacheCoordinationAssertions.VerifyResourceLimitsAsync(new FreeRedisCacheManager(_client,
            new CacheConfiguration("resource-free-" + Guid.NewGuid().ToString("N"), "free", batchSize: 2,
                resources: new CacheResourceOptions(1, 8, 32))));

    /// <summary>
    /// 自适应分批后列表读取与流式读取保持相同语义。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public Task BatchPayloadSplitting_PreservesListAndStreamSemantics() =>
        CacheCoordinationAssertions.VerifyAdaptivePayloadSplittingAsync(new FreeRedisCacheManager(_client,
            new CacheConfiguration("adaptive-free-" + Guid.NewGuid().ToString("N"), "free", batchSize: 2,
                resources: new CacheResourceOptions(maxBatchPayloadBytes: 32))));

    /// <summary>
    /// 验证显式写入会阻止较早开始的回源结果覆盖缓存。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public Task ExplicitMutations_RejectOlderFactoryCommit() => CacheCoordinationAssertions.VerifyMutationsWinAsync(
        new FreeRedisCacheManager(_client, new CacheConfiguration("race-free-" + Guid.NewGuid().ToString("N"), "free")));

    /// <summary>
    /// 回源超时后释放门控并拒绝迟到结果写回。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public Task FactoryTimeout_ReleasesGateAndRejectsLateCommit() => CacheCoordinationAssertions.VerifyTimeoutAsync(
        new FreeRedisCacheManager(_client, new CacheConfiguration("timeout-free-" + Guid.NewGuid().ToString("N"), "free",
            coordination: new CacheCoordinationOptions(retrievalTimeout: TimeSpan.FromMilliseconds(100)))));
}
