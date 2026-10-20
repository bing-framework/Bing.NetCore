using Bing.Caching;
using EasyCaching.Bus.Redis;
using EasyCaching.Core.Bus;
using EasyCaching.HybridCache;
using EasyCaching.InMemory;
using EasyCaching.Redis;
using EasyCaching.Serialization.SystemTextJson.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Bing.EasyCaching;

/// <summary>
/// 提供 EasyCaching Provider 的依赖注入注册方法。
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// 注册内存缓存及其 Bing 适配器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="configure">可选的内存 Provider 配置回调。</param>
    /// <returns>更新后的服务集合。</returns>
    public static IServiceCollection AddMemoryCaching(this IServiceCollection services,
        CacheConfiguration configuration, Action<InMemoryOptions> configure = null)
    {
        Validate(services, configuration);
        if (configuration.Topology != CacheTopology.SingleNode)
            throw new NotSupportedException("EasyCaching 内存缓存只支持单节点拓扑。");
        services.AddEasyCaching(options => options.UseInMemory(configure ?? (_ => { }), configuration.ProviderName));
        services.AddScoped<ILocalCache>(provider =>
            new MemoryCacheManager(provider.GetRequiredService<IEasyCachingProviderFactory>(), configuration));
        services.TryAddScoped<ICache>(provider =>
            new MemoryCacheManager(provider.GetRequiredService<IEasyCachingProviderFactory>(), configuration));
        return services;
    }

    /// <summary>
    /// 注册 Redis 缓存及其 Bing 适配器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="configure">Redis Provider 配置回调。</param>
    /// <returns>更新后的服务集合。</returns>
    public static IServiceCollection AddRedisCaching(this IServiceCollection services,
        CacheConfiguration configuration, Action<RedisOptions> configure)
    {
        Validate(services, configuration);
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        var serializerName = configuration.ProviderName + ":json";
        services.AddEasyCaching(options =>
        {
            options.WithSystemTextJson(serializerName);
            options.UseRedis(redis => ConfigureRedis(redis, configure, serializerName), configuration.ProviderName);
        });
        services.AddScoped<IRedisCache>(provider =>
            new RedisCacheManager(provider.GetRequiredService<IEasyCachingProviderFactory>(), configuration));
        services.TryAddScoped<ICache>(provider =>
            new RedisCacheManager(provider.GetRequiredService<IEasyCachingProviderFactory>(), configuration));
        return services;
    }

    /// <summary>
    /// 注册由多个独立 Redis 节点组成的客户端分片缓存。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">客户端分片缓存配置。</param>
    /// <param name="configureShards">各分片节点的 Redis 配置回调。</param>
    /// <returns>更新后的服务集合。</returns>
    public static IServiceCollection AddShardedRedisCaching(this IServiceCollection services,
        CacheConfiguration configuration, IReadOnlyList<Action<RedisOptions>> configureShards)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (configuration.Topology != CacheTopology.ClientSharded)
            throw new ArgumentException("客户端分片注册要求 ClientSharded 拓扑。", nameof(configuration));
        if (configureShards == null)
            throw new ArgumentNullException(nameof(configureShards));
        if (configureShards.Count != configuration.ExpectedShardCount)
            throw new ArgumentException("节点配置数量与预期分片数不一致。", nameof(configureShards));
        var shardConfigurations = configureShards.ToArray();
        if (shardConfigurations.Any(configure => configure == null))
            throw new ArgumentException("分片节点配置不能为空。", nameof(configureShards));
        var names = Enumerable.Range(0, shardConfigurations.Length)
            .Select(index => configuration.ProviderName + ":shard:" + index).ToArray();
        var serializerName = configuration.ProviderName + ":json";
        services.AddEasyCaching(options =>
        {
            options.WithSystemTextJson(serializerName);
            for (var index = 0; index < names.Length; index++)
            {
                var shard = shardConfigurations[index];
                options.UseRedis(redis => ConfigureRedis(redis, shard, serializerName), names[index]);
            }
        });
        services.AddScoped<IRedisCache>(provider => CreateShardedCache(provider, configuration, names));
        services.TryAddScoped<ICache>(provider => CreateShardedCache(provider, configuration, names));
        return services;
    }

    /// <summary>
    /// 根据注册的分片 Provider 创建缓存管理器。
    /// </summary>
    /// <param name="services">服务提供器。</param>
    /// <param name="configuration">客户端分片缓存配置。</param>
    /// <param name="names">分片 Provider 名称。</param>
    /// <returns>客户端分片缓存管理器。</returns>
    private static ShardedRedisCacheManager CreateShardedCache(IServiceProvider services,
        CacheConfiguration configuration, IReadOnlyList<string> names)
    {
        var factory = services.GetRequiredService<IEasyCachingProviderFactory>();
        return new ShardedRedisCacheManager(names.Select(factory.GetCachingProvider).ToArray(), configuration);
    }

    /// <summary>
    /// 注册带跨实例失效通知的客户端分片 Hybrid 缓存。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">客户端分片缓存配置。</param>
    /// <param name="configureShards">各分片节点的 Redis 配置回调。</param>
    /// <param name="configureBus">Redis 失效总线配置回调。</param>
    /// <param name="configureMemory">可选的本地内存 Provider 配置回调。</param>
    /// <param name="configureHybrid">可选的 Hybrid Provider 配置回调。</param>
    /// <param name="configureInvalidation">可选的失效投递配置回调。</param>
    /// <returns>更新后的服务集合。</returns>
    public static IServiceCollection AddShardedHybridCaching(this IServiceCollection services,
        CacheConfiguration configuration, IReadOnlyList<Action<RedisOptions>> configureShards,
        Action<RedisBusOptions> configureBus, Action<InMemoryOptions> configureMemory = null,
        Action<HybridCachingOptions> configureHybrid = null,
        Action<HybridInvalidationDeliveryOptions> configureInvalidation = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (configuration.Topology != CacheTopology.ClientSharded)
            throw new ArgumentException("客户端分片注册要求 ClientSharded 拓扑。", nameof(configuration));
        if (configureShards == null)
            throw new ArgumentNullException(nameof(configureShards));
        if (configureShards.Count != configuration.ExpectedShardCount)
            throw new ArgumentException("节点配置数量与预期分片数不一致。", nameof(configureShards));
        if (configureBus == null)
            throw new ArgumentNullException(nameof(configureBus));
        var shardConfigurations = configureShards.ToArray();
        if (shardConfigurations.Any(configure => configure == null))
            throw new ArgumentException("分片节点配置不能为空。", nameof(configureShards));
        var names = Enumerable.Range(0, shardConfigurations.Length)
            .Select(index => configuration.ProviderName + ":shard:" + index).ToArray();
        var distributedNames = names.Select(name => name + ":distributed").ToArray();
        var localNames = names.Select(name => name + ":local").ToArray();
        var busNames = names.Select(name => name + ":bus").ToArray();
        var serializerName = configuration.ProviderName + ":json";
        var topics = names.Select(name => name + ":invalidations").ToArray();
        var invalidationOptions = CreateInvalidationOptions(configureInvalidation);
        services.AddEasyCaching(options =>
        {
            options.WithSystemTextJson(serializerName);
            for (var index = 0; index < names.Length; index++)
            {
                var shard = shardConfigurations[index];
                var local = localNames[index];
                var distributed = distributedNames[index];
                var busName = busNames[index];
                options.WithRedisBus(bus =>
                {
                    configureBus(bus);
                    bus.SerializerName = serializerName;
                }, busName);
                options.UseInMemory(memory => ConfigureHybridMemory(memory, configureMemory), local);
                options.UseRedis(redis => ConfigureRedis(redis, shard, serializerName), distributed);
            }
        });
        services.TryAddSingleton<IHybridProviderFactory, DefaultHybridProviderFactory>();
        for (var index = 0; index < names.Length; index++)
        {
            var name = names[index];
            var local = localNames[index];
            var distributed = distributedNames[index];
            var busName = busNames[index];
            var topic = topics[index];
            var diagnosticTopic = topic;
            services.AddSingleton(provider => new HybridBusFailureTracker(
                provider.GetServices<IEasyCachingBus>().Single(item => item.Name == busName),
                () => provider.GetRequiredService<IEasyCachingProviderFactory>().GetCachingProvider(local).Flush(),
                provider.GetServices<IRedisSubscriberProvider>().Single(item => item.SubscriberName == busName)
                    .GetSubscriber().Multiplexer));
            services.AddSingleton(provider =>
            {
                var tracker = provider.GetServices<HybridBusFailureTracker>()
                    .Single(item => item.Name == busName);
                var distributedProvider = provider.GetRequiredService<IEasyCachingProviderFactory>()
                    .GetCachingProvider(distributed);
                return new HybridInvalidationDelivery(tracker,
                    distributedProvider.GetProviderInfo().Serializer, configuration, topic,
                    distributedProvider.Database as IDatabase, invalidationOptions, null,
                    provider.GetService<IHybridInvalidationDeadLetterAuthorizer>(),
                    provider.GetService<IHybridInvalidationDeadLetterAuditSink>());
            });
            services.AddSingleton<IHybridInvalidationDeliveryDiagnostics>(provider =>
                provider.GetServices<HybridInvalidationDelivery>()
                    .Single(item => item.Topic == diagnosticTopic));
            services.AddSingleton<IHybridInvalidationDeadLetterAdmin>(provider =>
                provider.GetServices<HybridInvalidationDelivery>()
                    .Single(item => item.Topic == diagnosticTopic));
            services.AddSingleton<IHybridCachingProvider>(provider =>
            {
                var hybrid = new HybridCachingOptions();
                configureHybrid?.Invoke(hybrid);
                hybrid.LocalCacheProviderName = local;
                hybrid.DistributedCacheProviderName = distributed;
                hybrid.ThrowIfDistributedCacheError = true;
                hybrid.TopicName = topic;
                return new HybridCachingProvider(name, hybrid,
                    provider.GetRequiredService<IEasyCachingProviderFactory>(),
                    provider.GetServices<HybridBusFailureTracker>().Single(item => item.Name == busName),
                    provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
            });
        }
        services.AddScoped<IRedisCache>(provider => CreateShardedHybridCache(provider, configuration,
            names, distributedNames, localNames, busNames, topics));
        services.TryAddScoped<ICache>(provider => CreateShardedHybridCache(provider, configuration,
            names, distributedNames, localNames, busNames, topics));
        return services;
    }

    /// <summary>
    /// 根据注册的 Provider 创建分片 Hybrid 缓存管理器。
    /// </summary>
    /// <param name="services">服务提供器。</param>
    /// <param name="configuration">客户端分片缓存配置。</param>
    /// <param name="names">Hybrid Provider 名称。</param>
    /// <param name="distributedNames">分布式 Provider 名称。</param>
    /// <param name="localNames">本地 Provider 名称。</param>
    /// <param name="busNames">失效总线名称。</param>
    /// <param name="topics">失效消息主题。</param>
    /// <returns>客户端分片 Hybrid 缓存管理器。</returns>
    private static ShardedRedisCacheManager CreateShardedHybridCache(IServiceProvider services,
        CacheConfiguration configuration, IReadOnlyList<string> names,
        IReadOnlyList<string> distributedNames, IReadOnlyList<string> localNames,
        IReadOnlyList<string> busNames, IReadOnlyList<string> topics)
    {
        var factory = services.GetRequiredService<IEasyCachingProviderFactory>();
        var hybridFactory = services.GetRequiredService<IHybridProviderFactory>();
        var buses = services.GetServices<HybridBusFailureTracker>().ToArray();
        var deliveries = services.GetServices<HybridInvalidationDelivery>().ToArray();
        var innerConfiguration = new CacheConfiguration(configuration.Namespace, configuration.ProviderName,
            configuration.DefaultExpiration, configuration.BatchSize, configuration.MaxConcurrency,
            configuration.Coordination, resources: configuration.Resources)
            .WithInputLimits(configuration.InputLimits)
            .WithSyncCleanupMode(configuration.SyncCleanupMode);
        var managers = new CacheManager[names.Count];
        var databases = new IDatabase[names.Count];
        for (var index = 0; index < names.Count; index++)
        {
            var topic = topics[index];
            var distributed = factory.GetCachingProvider(distributedNames[index]);
            var local = factory.GetCachingProvider(localNames[index]);
            var hybrid = hybridFactory.GetHybridCachingProvider(names[index]);
            var bus = buses.Single(item => item.Name == busNames[index]);
            var delivery = deliveries.Single(item => item.Topic == topic);
            databases[index] = distributed.Database as IDatabase ??
                throw new NotSupportedException("客户端分片需要 EasyCaching Redis Provider 提供 StackExchange.Redis 数据库。");
            var manager = new CacheManager(distributed, innerConfiguration, hybrid, local);
            manager.SetBusFailureTracker(bus);
            manager.SetCoordinator(new RedisLeaseCoordinator(distributed, innerConfiguration,
                physicalKey =>
                {
                    manager.MarkHybridMutation();
                    local.Remove(physicalKey);
                    delivery.Publish(
                        new[] { physicalKey }, configuration.Resources.MaxInvalidationMessageBytes);
                },
                async (physicalKey, token) =>
                {
                    manager.MarkHybridMutation();
                    await local.RemoveAsync(physicalKey, token);
                    await delivery.PublishAsync(
                        new[] { physicalKey }, configuration.Resources.MaxInvalidationMessageBytes, token);
                },
                physicalKeys =>
                {
                    manager.MarkHybridMutation();
                    local.RemoveAll(physicalKeys);
                    delivery.Publish(physicalKeys,
                        configuration.Resources.MaxInvalidationMessageBytes);
                },
                async (physicalKeys, token) =>
                {
                    manager.MarkHybridMutation();
                    await local.RemoveAllAsync(physicalKeys, token);
                    await delivery.PublishAsync(physicalKeys,
                        configuration.Resources.MaxInvalidationMessageBytes, token);
                }));
            managers[index] = manager;
        }
        return new ShardedRedisCacheManager(managers, databases, configuration);
    }

    /// <summary>
    /// 注册 Hybrid 缓存及 Redis 失效总线。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="distributedProviderName">分布式 Provider 名称。</param>
    /// <param name="configureRedis">Redis Provider 配置回调。</param>
    /// <param name="configureBus">Redis 失效总线配置回调。</param>
    /// <param name="configureMemory">可选的本地内存 Provider 配置回调。</param>
    /// <param name="configureHybrid">可选的 Hybrid Provider 配置回调。</param>
    /// <param name="configureInvalidation">可选的失效投递配置回调。</param>
    /// <returns>更新后的服务集合。</returns>
    public static IServiceCollection AddHybridCaching(this IServiceCollection services,
        CacheConfiguration configuration, string distributedProviderName,
        Action<RedisOptions> configureRedis, Action<RedisBusOptions> configureBus,
        Action<InMemoryOptions> configureMemory = null, Action<HybridCachingOptions> configureHybrid = null,
        Action<HybridInvalidationDeliveryOptions> configureInvalidation = null)
    {
        Validate(services, configuration);
        if (string.IsNullOrWhiteSpace(distributedProviderName))
            throw new ArgumentException("分布式 Provider 名称不能为空。", nameof(distributedProviderName));
        if (configureRedis == null)
            throw new ArgumentNullException(nameof(configureRedis));
        if (configureBus == null)
            throw new ArgumentNullException(nameof(configureBus));
        var localName = configuration.ProviderName + ":local";
        var serializerName = configuration.ProviderName + ":json";
        var busName = configuration.ProviderName + ":bus";
        var topic = configuration.ProviderName + ":invalidations";
        var invalidationOptions = CreateInvalidationOptions(configureInvalidation);
        services.AddEasyCaching(options =>
        {
            options.WithSystemTextJson(serializerName);
            options.UseInMemory(memory => ConfigureHybridMemory(memory, configureMemory), localName);
            options.UseRedis(redis => ConfigureRedis(redis, configureRedis, serializerName), distributedProviderName);
            options.WithRedisBus(bus =>
            {
                configureBus(bus);
                bus.SerializerName = serializerName;
            }, busName);
        });
        services.TryAddSingleton<IHybridProviderFactory, DefaultHybridProviderFactory>();
        services.AddSingleton(provider => new HybridBusFailureTracker(
            provider.GetServices<IEasyCachingBus>().Single(bus => bus.Name == busName),
            () => provider.GetRequiredService<IEasyCachingProviderFactory>().GetCachingProvider(localName).Flush(),
            provider.GetServices<IRedisSubscriberProvider>().Single(item => item.SubscriberName == busName)
                .GetSubscriber().Multiplexer));
        services.AddSingleton(provider =>
        {
            var tracker = provider.GetServices<HybridBusFailureTracker>().Single(bus => bus.Name == busName);
            var distributed = provider.GetRequiredService<IEasyCachingProviderFactory>()
                .GetCachingProvider(distributedProviderName);
            return new HybridInvalidationDelivery(tracker, distributed.GetProviderInfo().Serializer,
                configuration, topic, distributed.Database as IDatabase, invalidationOptions, null,
                provider.GetService<IHybridInvalidationDeadLetterAuthorizer>(),
                provider.GetService<IHybridInvalidationDeadLetterAuditSink>());
        });
        services.AddSingleton<IHybridInvalidationDeliveryDiagnostics>(provider =>
            provider.GetRequiredService<HybridInvalidationDelivery>());
        services.AddSingleton<IHybridInvalidationDeadLetterAdmin>(provider =>
            provider.GetRequiredService<HybridInvalidationDelivery>());
        services.AddSingleton<IHybridCachingProvider>(provider =>
        {
            var hybrid = new HybridCachingOptions();
            configureHybrid?.Invoke(hybrid);
            hybrid.LocalCacheProviderName = localName;
            hybrid.DistributedCacheProviderName = distributedProviderName;
            hybrid.ThrowIfDistributedCacheError = true;
            hybrid.TopicName = topic;
            return new HybridCachingProvider(configuration.ProviderName, hybrid,
                provider.GetRequiredService<IEasyCachingProviderFactory>(),
                provider.GetServices<HybridBusFailureTracker>().Single(bus => bus.Name == busName),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        });
        services.AddScoped<IRedisCache>(provider => new HybridCacheManager(
            provider.GetRequiredService<IEasyCachingProviderFactory>(),
             provider.GetRequiredService<IHybridProviderFactory>(), configuration, distributedProviderName,
             localName, provider.GetServices<HybridBusFailureTracker>().Single(bus => bus.Name == busName), topic,
             provider.GetRequiredService<HybridInvalidationDelivery>()));
        services.TryAddScoped<ICache>(provider => new HybridCacheManager(
            provider.GetRequiredService<IEasyCachingProviderFactory>(),
             provider.GetRequiredService<IHybridProviderFactory>(), configuration, distributedProviderName,
             localName, provider.GetServices<HybridBusFailureTracker>().Single(bus => bus.Name == busName), topic,
             provider.GetRequiredService<HybridInvalidationDelivery>()));
        return services;
    }

    /// <summary>
    /// 配置 Hybrid 本地内存 Provider。
    /// </summary>
    /// <param name="options">内存 Provider 选项。</param>
    /// <param name="configure">可选的配置回调。</param>
    private static void ConfigureHybridMemory(InMemoryOptions options, Action<InMemoryOptions> configure)
    {
        configure?.Invoke(options);
        // 本地回填不能通过随机延长过期时间超过 Redis 剩余 TTL。
        options.MaxRdSecond = 0;
    }

    /// <summary>
    /// 创建经过校验的失效投递配置。
    /// </summary>
    /// <param name="configure">可选的配置回调。</param>
    /// <returns>校验后的失效投递配置副本。</returns>
    private static HybridInvalidationDeliveryOptions CreateInvalidationOptions(
        Action<HybridInvalidationDeliveryOptions> configure)
    {
        var options = new HybridInvalidationDeliveryOptions();
        configure?.Invoke(options);
        return options.CloneValidated();
    }

    /// <summary>
    /// 配置 Redis Provider 及其序列化器名称。
    /// </summary>
    /// <param name="options">Redis Provider 选项。</param>
    /// <param name="configure">Redis Provider 配置回调。</param>
    /// <param name="serializerName">序列化器注册名称。</param>
    private static void ConfigureRedis(RedisOptions options, Action<RedisOptions> configure, string serializerName)
    {
        configure(options);
        if (!string.IsNullOrEmpty(options.DBConfig?.KeyPrefix))
            throw new ArgumentException("Redis KeyPrefix 必须为空；Bing 缓存已管理命名空间。", nameof(configure));
        options.SerializerName = serializerName;
    }

    /// <summary>
    /// 校验缓存服务注册参数。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">缓存实例配置。</param>
    private static void Validate(IServiceCollection services, CacheConfiguration configuration)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (configuration.Topology == CacheTopology.ClientSharded)
            throw new NotSupportedException("EasyCaching 适配器尚不支持客户端分片拓扑。");
    }
}
