using Bing.Caching.FusionCache.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache;

/// <summary>
/// 注册可与现有 ICache Provider 并存的 FusionCache 混合缓存门面。
/// </summary>
/// <remarks>
/// 底层组件通过官方 builder 配置，生命周期由依赖注入容器或调用方自行管理。
/// </remarks>
public static class FusionCachingServiceCollectionExtensions
{
    /// <summary>
    /// 注册指定名称的混合缓存实例。
    /// </summary>
    /// <param name="services">目标服务集合。</param>
    /// <param name="name">缓存实例名称，用于按名称解析并参与键前缀隔离。</param>
    /// <param name="configure">配置命名空间、输入限制和回源预算的委托。</param>
    /// <param name="configureFusion">可选的原生 FusionCache builder 配置委托。</param>
    /// <returns>当前服务集合，便于链式调用。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> 或 <paramref name="configure" /> 为 <see langword="null" /> 时抛出。</exception>
    /// <exception cref="ArgumentException">实例名称或命名空间为空或包含无效 UTF-16 字符时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">输入长度、回源预算或队列超时无效时抛出。</exception>
    /// <exception cref="InvalidOperationException">名称重复或重复注册默认实例时抛出。</exception>
    /// <remarks>
    /// 仅 <see cref="BingFusionCacheOptions.UseAsDefault" /> 为 <see langword="true" /> 时建立无名 <see cref="IHybridCache" /> 别名；
    /// 原生 builder 执行完所有配置回调后会再次校验身份、标签和保留期契约。
    /// </remarks>
    public static IServiceCollection AddBingFusionCache(this IServiceCollection services, string name,
        Action<BingFusionCacheOptions> configure, Action<IFusionCacheBuilder>? configureFusion = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        FusionCacheKeyNormalizer.Validate(name, 256, nameof(name));
        var configured = new BingFusionCacheOptions();
        configure(configured);
        var options = configured.Snapshot();
        var registrations = services.Where(x => x.ServiceType == typeof(FusionCacheRegistration))
            .Select(x => (FusionCacheRegistration)x.ImplementationInstance!).ToArray();
        if (registrations.Any(x => x.Name == name))
            throw new InvalidOperationException($"混合缓存名称已注册：{name}。");
        if (options.UseAsDefault && (registrations.Any(x => x.Options.UseAsDefault) ||
            services.Any(x => x.ServiceType == typeof(IHybridCache) && !x.IsKeyedService)))
            throw new InvalidOperationException("只能显式注册一个默认混合缓存实例。");

        var prefix = FusionCacheKeyNormalizer.Prefix(options.Namespace, name);
        var nativeOptions = new FusionCacheOptions
        {
            CacheName = name,
            CacheKeyPrefix = prefix,
            BackplaneChannelPrefix = prefix + "bus",
            WaitForInitialBackplaneSubscribe = true,
            RemoveByTagBehavior = RemoveByTagBehavior.Remove,
            AutoRecoveryMaxItems = 1000,
            DefaultEntryOptions = new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(5),
                FactoryHardTimeout = TimeSpan.FromMinutes(2),
                IsFailSafeEnabled = false,
                EagerRefreshThreshold = null,
                AllowTimedOutFactoryBackgroundCompletion = false,
                AllowBackgroundDistributedCacheOperations = false,
                AllowBackgroundBackplaneOperations = false,
                ReThrowDistributedCacheExceptions = true,
                ReThrowSerializationExceptions = true,
                ReThrowBackplaneExceptions = true
            }
        };
        // 元数据沿用上游长保留窗口，不能使用业务 TTL 覆盖标签/Clear 墓碑。
        nativeOptions.TagsDefaultEntryOptions.AllowBackgroundDistributedCacheOperations = false;
        nativeOptions.TagsDefaultEntryOptions.AllowBackgroundBackplaneOperations = false;
        nativeOptions.TagsDefaultEntryOptions.ReThrowDistributedCacheExceptions = true;
        nativeOptions.TagsDefaultEntryOptions.ReThrowBackplaneExceptions = true;
        nativeOptions.TagsDefaultEntryOptions.ReThrowSerializationExceptions = true;
        var registration = new FusionCacheRegistration(name, options);
        var builder = services.AddFusionCache(name).WithOptions(nativeOptions);
        configureFusion?.Invoke(builder);
        FusionCacheStartupBackplane.Wrap(builder);
        // 在官方 builder 执行所有用户 options 回调后验证，避免覆盖隔离字段。
        builder.WithOptions((_, finalOptions) =>
        {
            FusionCacheRegistrationValidator.Validate(builder, finalOptions, name, prefix);
            registration.TagOptions = finalOptions.TagsDefaultEntryOptions.Duplicate();
        });
        builder.SetupDefaultEntryOptionsAction += FusionCacheRegistrationValidator.ValidateEntryOptions;

        services.AddSingleton(registration);
        services.AddSingleton<FusionCacheManager>(provider =>
            new FusionCacheManager(
                provider.GetRequiredService<IFusionCacheProvider>().GetCache(name), options, registration.TagOptions!,
                provider.GetService<ILogger<FusionCacheManager>>()));
        services.TryAddSingleton<IHybridCacheProvider, FusionCacheProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FusionCacheStartupValidator>());
        if (options.UseAsDefault)
            services.AddSingleton(provider => provider.GetRequiredService<IHybridCacheProvider>().GetCache(name));
        return services;
    }
}

/// <summary>
/// 保存单个命名缓存注册时的冻结配置。
/// </summary>
/// <param name="Name">缓存实例注册名称。</param>
/// <param name="Options">注册时复制的 Bing 缓存配置。</param>
internal sealed record FusionCacheRegistration(string Name, BingFusionCacheOptions Options)
{
    /// <summary>
    /// 获取或设置原生 builder 完成配置后捕获的标签与清除元数据选项。
    /// </summary>
    internal FusionCacheEntryOptions? TagOptions { get; set; }
}

/// <summary>
/// 宿主启动时解析全部命名缓存实例。
/// </summary>
/// <remarks>
/// 使身份、标签和保留期等错误配置在服务请求前暴露。
/// </remarks>
/// <param name="provider">按名称解析缓存实例的提供程序。</param>
internal sealed class FusionCacheStartupValidator(IHybridCacheProvider provider) : IHostedService
{
    /// <inheritdoc />
    /// <remarks>
    /// 构造时已解析全部命名实例，此入口仅用于触发宿主的启动阶段校验。
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = provider; // 构造时已解析全部命名实例。
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
