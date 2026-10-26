using Bing.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Samples.FusionCache;

/// <summary>
/// 在宿主启动阶段尽力预热少量热门产品；部分失败或预算耗尽会明确报告，但允许宿主继续启动。
/// </summary>
/// <param name="services">用于解析命名缓存与创建独立回源作用域的根容器。</param>
/// <param name="factoryLifetime">跟踪真实工厂生命周期的排空器。</param>
/// <param name="applicationLifetime">宿主关闭令牌来源。</param>
/// <param name="settings">本次演示的预算与故障开关。</param>
internal sealed class SampleWarmupHostedService(IServiceProvider services, SampleFactoryLifetime factoryLifetime,
    IHostApplicationLifetime applicationLifetime, SampleWarmupSettings settings) : IHostedService
{
    /// <summary>
    /// 模拟由业务维护的有限热门清单。实际项目可从配置或有界查询获取，不枚举全表。
    /// </summary>
    internal static readonly IReadOnlyList<string> HotKeys =
        Enumerable.Range(1, 12).Select(id => $"warmup:product:{id}").ToArray();

    /// <summary>
    /// 本次预热实际进入业务工厂的次数，区别于包含缓存命中的成功次数。
    /// </summary>
    private int _factoryCalls;

    /// <summary>
    /// 获取最近一次预热结果；开始前为空。
    /// </summary>
    internal SampleWarmupResult? Result { get; private set; }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, applicationLifetime.ApplicationStopping);
        var cache = services.GetRequiredService<IHybridCacheProvider>().GetCache("sample");
        Result = await SampleCacheWarmup.RunAsync(cache, HotKeys, LoadAsync,
            batchSize: 4, budget: settings.Budget, cancellationToken: linked.Token);
        Console.WriteLine($"warmup: requested={Result.Requested}, started={Result.Started}, " +
            $"succeeded={Result.Succeeded}, failed={Result.Failed}, cancelled={Result.Cancelled}, " +
            $"notStarted={Result.NotStarted}, timedOut={Result.TimedOut}, " +
            $"callerCancelled={Result.CallerCancelled}, factoryCalls={_factoryCalls}");
        // 超时允许继续启动；外部取消必须传给宿主，不能误报启动成功。
        linked.Token.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 只有真实回源才创建作用域；取消或异常时也会异步释放作用域。
    /// </summary>
    private Task<string> LoadAsync(string key, CancellationToken token) => factoryLifetime.RunAsync(async ct =>
    {
        Interlocked.Increment(ref _factoryCalls);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<SampleProductReader>();
        var value = await reader.ReadAsync(ct);
        if (settings.InjectFailure && key == HotKeys[2])
            throw new InvalidOperationException("Simulated single-product origin failure.");
        return $"{key}:{value}";
    }, token);

    /// <inheritdoc />
    /// <remarks>
    /// StartAsync 已等待所有预热调用结束；工厂排空由最后注册的 SampleFactoryLifetime 负责。
    /// </remarks>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// 样例专用的预热演示配置，不改变缓存包的默认行为。
/// </summary>
/// <param name="Budget">整个预热过程的协作取消预算。</param>
/// <param name="InjectFailure">是否让第三个产品回源失败。</param>
internal sealed record SampleWarmupSettings(TimeSpan Budget, bool InjectFailure);
