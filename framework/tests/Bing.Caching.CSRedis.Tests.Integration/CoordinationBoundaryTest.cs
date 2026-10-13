using Bing.Caching.ContractTests;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// CSRedis 缓存协调边界测试。
/// </summary>
/// <remarks>
/// 覆盖回源合并、资源预算、批量载荷拆分、显式修改和超时提交处理。
/// </remarks>
public class CoordinationBoundaryTest
{
    /// <summary>
    /// 保存由测试夹具提供、用于创建协调边界断言所需缓存管理器的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;
    /// <summary>
    /// 初始化一个 <see cref="CoordinationBoundaryTest"/> 类型的实例。
    /// </summary>
    /// <param name="client">供各边界测试创建缓存管理器的 Redis 客户端。</param>
    public CoordinationBoundaryTest(CSRedisClient client) => _client = client;

    /// <summary>
    /// 验证相同键回源任务的协调。
    /// </summary>
    /// <remarks>
    /// 检查相同键请求合并、零排队资源限制，以及租约丢失时取消回源任务。
    /// </remarks>
    [Bing.Test.Shared.IntegrationFact]
    public Task SameKeyMergeWithZeroQueue_AndLeaseLossCancelsFactory() =>
        CacheCoordinationAssertions.VerifySameKeyMergeAndLeaseLossAsync(new CSRedisCacheManager(_client,
            new CacheConfiguration("merge-cs-" + Guid.NewGuid().ToString("N"), "cs",
                resources: new CacheResourceOptions(1, 0),
                coordination: new CacheCoordinationOptions(renewalInterval: TimeSpan.FromMilliseconds(50)))));

    /// <summary>
    /// 验证回源并发和批量载荷的资源限制。
    /// </summary>
    /// <remarks>
    /// 不同键的回源任务和批量载荷都必须遵守配置的资源上限。
    /// </remarks>
    [Bing.Test.Shared.IntegrationFact]
    public Task DifferentKeyFactoriesAndBatchPayload_RespectConfiguredResourceLimits() =>
        CacheCoordinationAssertions.VerifyResourceLimitsAsync(new CSRedisCacheManager(_client,
            new CacheConfiguration("resource-cs-" + Guid.NewGuid().ToString("N"), "cs", batchSize: 2,
                resources: new CacheResourceOptions(1, 8, 32))));

    /// <summary>
    /// 验证批量载荷拆分后的列表和流式读取语义。
    /// </summary>
    /// <remarks>
    /// 批量载荷超过预算时应分批处理，并保持列表和流式读取结果一致。
    /// </remarks>
    [Bing.Test.Shared.IntegrationFact]
    public Task BatchPayloadSplitting_PreservesListAndStreamSemantics() =>
        CacheCoordinationAssertions.VerifyAdaptivePayloadSplittingAsync(new CSRedisCacheManager(_client,
            new CacheConfiguration("adaptive-cs-" + Guid.NewGuid().ToString("N"), "cs", batchSize: 2,
                resources: new CacheResourceOptions(maxBatchPayloadBytes: 32))));

    /// <summary>
    /// 验证显式修改与旧回源提交的先后关系。
    /// </summary>
    /// <remarks>
    /// 显式缓存修改应覆盖较早回源任务的提交结果。
    /// </remarks>
    [Bing.Test.Shared.IntegrationFact]
    public Task ExplicitMutations_RejectOlderFactoryCommit() => CacheCoordinationAssertions.VerifyMutationsWinAsync(
        new CSRedisCacheManager(_client, new CacheConfiguration("race-cs-" + Guid.NewGuid().ToString("N"), "cs")));

    /// <summary>
    /// 验证回源超时后的协调门清理。
    /// </summary>
    /// <remarks>
    /// 超时后应释放协调门，并拒绝迟到的回源结果提交。
    /// </remarks>
    [Bing.Test.Shared.IntegrationFact]
    public Task FactoryTimeout_ReleasesGateAndRejectsLateCommit() => CacheCoordinationAssertions.VerifyTimeoutAsync(
        new CSRedisCacheManager(_client, new CacheConfiguration("timeout-cs-" + Guid.NewGuid().ToString("N"), "cs",
            coordination: new CacheCoordinationOptions(retrievalTimeout: TimeSpan.FromMilliseconds(100)))));
}
