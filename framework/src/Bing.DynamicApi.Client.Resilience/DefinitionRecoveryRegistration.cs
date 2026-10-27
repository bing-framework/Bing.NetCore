using System.Net;
using Bing.DynamicApi.Contracts;
using Bing.Resilience;
using Bing.Resilience.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Bing.DynamicApi.Client.Resilience;

internal sealed record DefinitionRecoveryRegistration(string RemoteName, string TransportName,
    BingDynamicApiDefinitionRecoveryOptions Options, TimeSpan Timeout);
internal static class DefinitionRecoveryRegistrationExtensions
{
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
internal sealed class DefinitionTransportFilter(IEnumerable<DefinitionRecoveryRegistration> registrations) : IHttpMessageHandlerBuilderFilter
{
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
internal sealed class DefinitionSourceStartupValidator(IEnumerable<DefinitionRecoveryRegistration> registrations,
    IEnumerable<IBingDynamicApiDefinitionSource> sources, IHttpClientFactory clients) : IHostedService
{
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
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
