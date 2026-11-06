using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace Bing.Caching.FusionCache.Internal;

/// <summary>
/// 记录首次 Backplane 订阅结果的转发包装器。
/// </summary>
/// <remarks>
/// 转发官方 Backplane 协议但不取得底层组件的释放所有权，仅用于让缓存门面在启动阶段确认订阅已经成功。
/// </remarks>
/// <param name="inner">实际执行订阅、退订和发布的外部 Backplane。</param>
internal sealed class FusionCacheStartupBackplane(IFusionCacheBackplane inner) : IFusionCacheBackplane
{
    /// <summary>
    /// 获取首次订阅是否已经成功完成。
    /// </summary>
    internal bool IsSubscribed { get; private set; }

    /// <summary>
    /// 获取最近一次订阅失败异常。
    /// </summary>
    /// <remarks>订阅成功时为 <see langword="null" />。</remarks>
    internal Exception? SubscriptionError { get; private set; }

    /// <summary>
    /// 为 builder 已显式配置的 Backplane 安装订阅结果包装器。
    /// </summary>
    /// <param name="builder">已完成用户配置的官方 FusionCache builder。</param>
    /// <remarks>
    /// 未配置任何 Backplane 时保持原状；使用注册服务、工厂或实例三种入口时均保留原始解析语义。
    /// </remarks>
    internal static void Wrap(IFusionCacheBuilder builder)
    {
        var registered = builder.UseRegisteredBackplane;
        var serviceKey = builder.BackplaneServiceKey;
        var factory = builder.BackplaneFactory;
        var instance = builder.Backplane;
        if (!registered && factory == null && instance == null) return;
        builder.WithBackplane(provider =>
        {
            var backplane = registered
                ? serviceKey == null ? provider.GetService<IFusionCacheBackplane>() : provider.GetKeyedService<IFusionCacheBackplane>(serviceKey)
                : factory != null ? factory(provider) : instance;
            return new FusionCacheStartupBackplane(backplane ?? throw new InvalidOperationException("已配置 Backplane，但未能解析组件。"));
        });
    }

    /// <inheritdoc />
    /// <exception cref="Exception">底层 Backplane 订阅失败时重新抛出。</exception>
    public void Subscribe(BackplaneSubscriptionOptions options)
    {
        try { inner.Subscribe(options); IsSubscribed = true; SubscriptionError = null; }
        catch (Exception error) { SubscriptionError = error; throw; }
    }

    /// <inheritdoc />
    /// <exception cref="Exception">底层 Backplane 异步订阅失败时重新抛出。</exception>
    public async ValueTask SubscribeAsync(BackplaneSubscriptionOptions options)
    {
        try { await inner.SubscribeAsync(options).ConfigureAwait(false); IsSubscribed = true; SubscriptionError = null; }
        catch (Exception error) { SubscriptionError = error; throw; }
    }

    /// <inheritdoc />
    public void Unsubscribe() { inner.Unsubscribe(); IsSubscribed = false; }

    /// <inheritdoc />
    public async ValueTask UnsubscribeAsync() { await inner.UnsubscribeAsync().ConfigureAwait(false); IsSubscribed = false; }

    /// <inheritdoc />
    public void Publish(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default) => inner.Publish(message, options, token);

    /// <inheritdoc />
    public ValueTask PublishAsync(BackplaneMessage message, FusionCacheEntryOptions options, CancellationToken token = default) => inner.PublishAsync(message, options, token);
}
