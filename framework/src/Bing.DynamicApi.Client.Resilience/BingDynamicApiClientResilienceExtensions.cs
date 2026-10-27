using Bing.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.DynamicApi.Client.Resilience;

/// <summary>运行时动态 API 客户端的恢复及完整调用预算。</summary>
public sealed class BingDynamicApiClientResilienceOptions
{
    /// <summary>HTTP 发送恢复配置；业务调用仍需明确的 CanRetryRequest 许可。</summary>
    public BingHttpResilienceOptions Http { get; } = new();
    /// <summary>独立描述恢复；默认保留旧顺序下载路径。</summary>
    public BingDynamicApiDefinitionRecoveryOptions Definition { get; } = new();
    /// <summary>完整业务调用预算，覆盖描述等待、发送和内容读取；流只覆盖取得流。</summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>共享描述下载预算，独立于任意等待者。</summary>
    public TimeSpan DefinitionTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>将运行时动态客户端绑定到共享 HTTP 恢复管线。</summary>
public static class BingDynamicApiClientResilienceExtensions
{
    /// <summary>绑定远程服务，启用 HttpPipeline 并返回实际具名 builder。</summary>
    /// <remarks>远程服务必须已通过 AddRemoteService 配置；旧 MaxRetries 必须为零。</remarks>
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
