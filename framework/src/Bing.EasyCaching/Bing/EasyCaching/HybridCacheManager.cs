using Bing.Caching;
using EasyCaching.Core.Bus;
using StackExchange.Redis;

namespace Bing.EasyCaching;

/// <summary>
/// 通过 EasyCaching Hybrid Provider 同时维护本地层和分布式层。
/// </summary>
public sealed class HybridCacheManager : CacheManager, IRedisCache
{
    /// <summary>
    /// 初始化一个 <see cref="HybridCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="providerFactory">EasyCaching Provider 工厂。</param>
    /// <param name="hybridFactory">Hybrid Provider 工厂。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="distributedProviderName">分布式 Provider 名称。</param>
    /// <param name="localProviderName">本地 Provider 名称。</param>
    /// <param name="bus">Hybrid 失效总线。</param>
    /// <param name="topic">失效消息主题。</param>
    /// <param name="delivery">可选的失效投递器。</param>
    internal HybridCacheManager(IEasyCachingProviderFactory providerFactory, IHybridProviderFactory hybridFactory,
        CacheConfiguration configuration, string distributedProviderName, string localProviderName,
        IEasyCachingBus bus, string topic, HybridInvalidationDelivery delivery = null)
        : base(providerFactory?.GetCachingProvider(distributedProviderName), configuration,
            hybridFactory?.GetHybridCachingProvider(configuration?.ProviderName),
            providerFactory?.GetCachingProvider(localProviderName))
    {
        if (string.IsNullOrWhiteSpace(distributedProviderName))
            throw new ArgumentException("分布式 Provider 名称不能为空。", nameof(distributedProviderName));
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (bus is not HybridBusFailureTracker tracker)
            throw new ArgumentException("Hybrid Provider 与缓存管理器必须共用 HybridBusFailureTracker。", nameof(bus));
        SetBusFailureTracker(tracker);
        var distributed = providerFactory.GetCachingProvider(distributedProviderName);
        var local = providerFactory.GetCachingProvider(localProviderName);
        var serializer = distributed.GetProviderInfo().Serializer;
        var invalidation = delivery ?? new HybridInvalidationDelivery(tracker, serializer, configuration, topic,
            distributed.Database as IDatabase, new HybridInvalidationDeliveryOptions());
        if (!string.Equals(invalidation.Topic, topic, StringComparison.Ordinal))
            throw new ArgumentException("Hybrid 失效投递器主题与缓存管理器主题不一致。", nameof(delivery));
        SetCoordinator(new RedisLeaseCoordinator(distributed, configuration,
            physicalKey =>
            {
                MarkHybridMutation();
                local.Remove(physicalKey);
                invalidation.Publish(
                    new[] { physicalKey }, configuration.Resources.MaxInvalidationMessageBytes);
            },
            async (physicalKey, token) =>
            {
                MarkHybridMutation();
                await local.RemoveAsync(physicalKey, token);
                await invalidation.PublishAsync(
                    new[] { physicalKey }, configuration.Resources.MaxInvalidationMessageBytes, token);
            },
            physicalKeys =>
            {
                MarkHybridMutation();
                local.RemoveAll(physicalKeys);
                invalidation.Publish(physicalKeys,
                    configuration.Resources.MaxInvalidationMessageBytes);
            },
            async (physicalKeys, token) =>
            {
                MarkHybridMutation();
                await local.RemoveAllAsync(physicalKeys, token);
                await invalidation.PublishAsync(physicalKeys,
                    configuration.Resources.MaxInvalidationMessageBytes, token);
            }));
    }
}
