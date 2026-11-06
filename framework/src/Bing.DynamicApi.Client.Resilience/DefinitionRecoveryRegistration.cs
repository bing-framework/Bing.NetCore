using System.Net;
using Bing.DynamicApi.Contracts;
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Bing.DynamicApi.Client.Resilience;

/// <summary>
/// 远程服务的描述恢复注册信息。
/// </summary>
/// <param name="RemoteName">动态 API 远程服务名称。</param>
/// <param name="TransportName">描述下载的具名传输客户端名称。</param>
/// <param name="Options">注册时保存的配置。</param>
/// <param name="Timeout">操作允许使用的时限。</param>
internal sealed record DefinitionRecoveryRegistration(string RemoteName, string TransportName,
    BingDynamicApiDefinitionRecoveryOptions Options, TimeSpan Timeout);
/// <summary>
/// 提供动态 API 定义恢复服务的注册扩展。
/// </summary>
internal static class DefinitionRecoveryRegistrationExtensions
{
    /// <summary>
    /// 根据动态 API 定义恢复选项注册下载管线及远程描述源。
    /// </summary>
    /// <param name="services">接收恢复服务注册的容器。</param>
    /// <param name="remoteName">远程服务注册名称。</param>
    /// <param name="options">动态 API 客户端弹性配置。</param>
    /// <remarks>
    /// 未启用定义恢复、对冲、清单候选和服务发现引导时不添加相关服务。
    /// </remarks>
    internal static void Register(IServiceCollection services, string remoteName, BingDynamicApiClientResilienceOptions options)
    {
        var configured = options.Definition;
        if (!configured.Enabled && configured.RecoveryMode != BingDynamicApiDefinitionRecoveryMode.Hedging &&
            !configured.UseManifestCandidates && !configured.UseDiscoveryBootstrap) return;
        var frozen = configured.Freeze();
        var registration = new DefinitionRecoveryRegistration(remoteName, remoteName + ".bing.definition", frozen, options.DefinitionTimeout);
        services.AddSingleton(registration);
        services.AddBingResiliencePipeline<BingDynamicApiDefinition>(registration.TransportName, pipeline =>
        {
            pipeline.ProviderName = options.Http.ProviderName;
            pipeline.Purpose = "definition.fetch";
            pipeline.ExecutionTimeout = options.DefinitionTimeout;
            if (frozen.RecoveryMode == BingDynamicApiDefinitionRecoveryMode.Hedging && frozen.MaxAttempts > 1)
            {
                pipeline.Hedging.Enabled = true;
                pipeline.Hedging.AllowParallelExecution = true;
                pipeline.Hedging.MaxHedgedAttempts = frozen.MaxAttempts - 1;
                pipeline.Hedging.Delay = frozen.Delay;
                pipeline.Hedging.ShouldHandle = a => a.Outcome.Exception is DefinitionCandidateException;
            }
            else
            {
                pipeline.Retry.MaxRetryAttempts = frozen.MaxAttempts - 1;
                pipeline.Retry.Delay = frozen.Delay; pipeline.Retry.MaxDelay = frozen.Delay; pipeline.Retry.UseJitter = false;
                pipeline.Retry.ShouldHandle = a => a.Outcome.Exception is DefinitionCandidateException;
            }
        });
        var transport = services.AddHttpClient(registration.TransportName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None });
        frozen.ConfigureTransport?.Invoke(transport);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, DefinitionTransportFilter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DefinitionSourceStartupValidator>());
        services.AddSingleton<IBingDynamicApiDefinitionSource>(sp => new HedgedDefinitionSource(registration,
            sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<IResiliencePipelineProvider>(),
            sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(),
            frozen.UseDiscoveryBootstrap ? sp.GetRequiredService<IServiceEndpointSource>() : null,
            frozen.UseManifestCandidates ? sp.GetRequiredService<IEndpointManifestSource>() : null));
    }
}
/// <summary>
/// 在 HTTP 客户端处理器管线中接入定义恢复传输。
/// </summary>
/// <param name="registrations">已注册的处理器或策略。</param>
internal sealed class DefinitionTransportFilter(IEnumerable<DefinitionRecoveryRegistration> registrations) : IHttpMessageHandlerBuilderFilter
{
    /// <inheritdoc />
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        var registration = registrations.SingleOrDefault(x => x.TransportName == builder.Name);
        if (registration is null) return;
        if (builder.AdditionalHandlers.Any(h => h.GetType().FullName is "Bing.Http.Resilience.BingHttpResilienceHandler" or
            "Microsoft.Extensions.Http.Resilience.ResilienceHandler" or "Microsoft.Extensions.Http.PolicyHttpMessageHandler"))
            throw new ResilienceConfigurationException("描述分支 transport 不能嵌套 HTTP 恢复调度。");
        var valid = builder.PrimaryHandler switch
        {
            SocketsHttpHandler handler => !handler.AllowAutoRedirect && !handler.UseCookies && handler.AutomaticDecompression == DecompressionMethods.None,
            HttpClientHandler handler => !handler.AllowAutoRedirect && !handler.UseCookies && handler.AutomaticDecompression == DecompressionMethods.None,
            _ => registration.Options.ValidateCustomTransport?.Invoke(builder.PrimaryHandler) == true
        };
        if (!valid) throw new ResilienceConfigurationException("描述 transport 必须关闭重定向、共享 Cookie 和自动解压；解压由有界读取器负责。");
    };
}
/// <summary>
/// 在宿主启动时校验已注册的动态 API 定义来源。
/// </summary>
/// <param name="registrations">已注册的处理器或策略。</param>
/// <param name="sources">用于初始化当前实例的sources值。</param>
/// <param name="clients">用于初始化当前实例的clients值。</param>
internal sealed class DefinitionSourceStartupValidator(IEnumerable<DefinitionRecoveryRegistration> registrations,
    IEnumerable<IBingDynamicApiDefinitionSource> sources, IHttpClientFactory clients) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in registrations)
        {
            if (sources.Count(x => string.Equals(x.RemoteName, registration.RemoteName, StringComparison.OrdinalIgnoreCase)) != 1)
                throw new ResilienceConfigurationException("描述 Source 必须唯一。");
            using var client = clients.CreateClient(registration.TransportName);
            if (client.Timeout != Timeout.InfiniteTimeSpan) throw new ResilienceConfigurationException("描述 transport 使用统一完整预算，不得另设 HttpClient.Timeout。");
        }
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
