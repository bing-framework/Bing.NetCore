using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.DynamicApi.Client.Resilience;

/// <summary>
/// 动态 API 客户端的恢复与超时配置。
/// </summary>
public sealed class BingDynamicApiClientResilienceOptions
{
    /// <summary>
    /// 获取HTTP 发送恢复配置。
    /// </summary>
    /// <remarks>业务调用仍需明确的 CanRetryRequest 许可。</remarks>
    public BingHttpResilienceOptions Http { get; } = new();
    /// <summary>
    /// 获取独立描述恢复。
    /// </summary>
    /// <remarks>默认保留旧顺序下载路径。</remarks>
    public BingDynamicApiDefinitionRecoveryOptions Definition { get; } = new();
    /// <summary>
    /// 获取或设置完整业务调用预算，覆盖描述等待、发送和内容读取。
    /// </summary>
    /// <remarks>流只覆盖取得流。默认值为 10 秒。</remarks>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>
    /// 获取或设置共享描述下载预算，独立于任意等待者。
    /// </summary>
    /// <remarks>默认值为 5 秒。</remarks>
    public TimeSpan DefinitionTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// 动态 API 客户端恢复管线注册扩展。
/// </summary>
/// <remarks>
/// 将运行时动态客户端绑定到共享 HTTP 恢复管线。
/// </remarks>
public static class BingDynamicApiClientResilienceExtensions
{
    /// <summary>
    /// 为动态 API 远程服务注册 HTTP 韧性客户端。
    /// </summary>
    /// <param name="services">接收 HTTP 客户端和韧性服务的依赖注入集合。</param>
    /// <param name="remoteName">要绑定韧性配置的远程服务名称。</param>
    /// <param name="configure">可选的 HTTP 重试、对冲和超时配置委托。</param>
    /// <returns>已注册远程服务 HTTP 客户端的构建器。</returns>
    /// <remarks>
    /// 重试和对冲仅对满足请求元数据、可重放性、HTTP 方法及流结果限制的调用开放。
    /// </remarks>
    public static IHttpClientBuilder AddBingDynamicApiClientResilience(this IServiceCollection services,
        string remoteName, Action<BingDynamicApiClientResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(remoteName))
            throw new ArgumentException("远程服务名称不能为空。", nameof(remoteName));
        if (services.Any(d => d.ImplementationInstance is BingDynamicApiHttpPipelineBinding b &&
            string.Equals(b.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"远程服务 '{remoteName}' 已注册恢复适配。");

        var options = new BingDynamicApiClientResilienceOptions();
        configure?.Invoke(options);
        if (options.OperationTimeout <= TimeSpan.Zero || options.OperationTimeout > TimeSpan.FromDays(1) ||
            options.DefinitionTimeout <= TimeSpan.Zero || options.DefinitionTimeout > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(configure), "调用预算必须大于零且不超过 1 天。");
        var operationTimeout = options.OperationTimeout;
        var definitionTimeout = options.DefinitionTimeout;
        var hostPermit = options.Http.CanRetryRequest;
        var hedgePermit = options.Http.CanHedgeRequest;
        var builder = services.AddHttpClient(remoteName);
        builder.AddBingHttpResilience("dynamic-api", http =>
        {
            options.Http.CopyTo(http);
            http.UseSequentialRetry = request => BingDynamicApiRequestMetadata.TryGet(request, out var metadata) &&
                metadata.Kind == BingDynamicApiRequestKind.Definition;
            http.CanHedgeRequest = request =>
                BingDynamicApiRequestMetadata.TryGet(request, out var metadata) &&
                string.Equals(metadata.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase) &&
                metadata.Kind == BingDynamicApiRequestKind.Invocation && metadata.IsReplayable &&
                !metadata.IsStreamResult && request.Method == HttpMethod.Get && hedgePermit?.Invoke(request) == true;
            // 元数据为不可变对象；这里只复制引用，不共享任何可变请求内容。
            http.Hedging.RegisterOption(new HttpRequestOptionsKey<BingDynamicApiRequestMetadata>("Bing.DynamicApi.RequestMetadata"), value => value);
            http.CanRetryRequest = request =>
                BingDynamicApiRequestMetadata.TryGet(request, out var metadata) &&
                string.Equals(metadata.RemoteName, remoteName, StringComparison.OrdinalIgnoreCase) &&
                metadata.IsReplayable && !metadata.IsStreamResult && request.Method == HttpMethod.Get &&
                (metadata.Kind == BingDynamicApiRequestKind.Definition || hostPermit?.Invoke(request) == true);
        });
        services.AddSingleton(new BingDynamicApiHttpPipelineBinding(remoteName, builder.Name));
        DefinitionRecoveryRegistrationExtensions.Register(services, remoteName, options);
        // PostConfigure 与注册顺序无关；保留旧重试冲突，由中性选项校验器报错。
        services.AddOptions<BingDynamicApiClientOptions>().PostConfigure(client =>
        {
            if (!client.RemoteServices.TryGetValue(remoteName, out var remote))
                throw new OptionsValidationException(Microsoft.Extensions.Options.Options.DefaultName, typeof(BingDynamicApiClientOptions),
                    new[] { $"未配置动态 API 远程服务 '{remoteName}'。" });
            remote.RetryMode = BingDynamicApiRetryMode.HttpPipeline;
            remote.OperationTimeout = operationTimeout;
            remote.DefinitionTimeout = definitionTimeout;
        }).ValidateOnStart();
        return builder;
    }

}
