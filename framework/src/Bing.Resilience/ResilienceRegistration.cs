using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bing.Resilience;

/// <summary>容错装配入口；不构造临时服务容器。</summary>
public sealed class BingResilienceBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;
    public BingResilienceBuilder UseDefaultProvider(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Provider 名称不能为空。", nameof(name));
        var existing = Services.FirstOrDefault(x => x.ServiceType == typeof(DefaultProvider));
        if (existing is not null) throw new ResilienceConfigurationException("默认 Provider 已指定。");
        Services.AddSingleton(new DefaultProvider(name));
        return this;
    }

    /// <summary>供可选适配包注册具名实现；不通过程序集扫描发现引擎。</summary>
    public BingResilienceBuilder AddProvider(IResilienceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(provider.Name)) throw new ResilienceConfigurationException("Provider 名称不能为空。");
        if (Services.Any(x => x.ServiceType == typeof(IResilienceProvider) && x.ImplementationInstance is IResilienceProvider p && p.Name == provider.Name))
            throw new ResilienceConfigurationException($"Provider '{provider.Name}' 重复注册。");
        Services.AddSingleton(provider);
        return this;
    }
}
internal sealed record DefaultProvider(string Name);

/// <summary>通用管线服务注册。</summary>
public static class ResilienceServiceCollectionExtensions
{
    public static BingResilienceBuilder AddBingResilience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ResilienceRegistry>();
        services.TryAddSingleton<IResiliencePipelineProvider>(sp =>
        {
            var registry = sp.GetRequiredService<ResilienceRegistry>();
            registry.Validate();
            return registry;
        });
        services.TryAddSingleton<IResilienceCircuitController>(sp => sp.GetRequiredService<ResilienceRegistry>());
        services.TryAddSingleton<IResilienceCircuitStateReader>(sp => sp.GetRequiredService<ResilienceRegistry>());
        services.TryAddSingleton<IResiliencePartitionedPipelineProvider>(sp => sp.GetRequiredService<ResilienceRegistry>());
        services.TryAddSingleton<IResilienceRuntimeLeaseProvider>(sp => sp.GetRequiredService<ResilienceRegistry>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ResilienceStartupValidator>());
        return new(services);
    }

    public static IServiceCollection AddBingResiliencePipeline<T>(this IServiceCollection services, string name, Action<ResiliencePipelineOptions<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ResiliencePipelineOptions<T>();
        configure(options);
        return AddDefinition(services, options.Freeze(name));
    }

    public static IServiceCollection AddBingResiliencePipeline(this IServiceCollection services, string name, Action<ResiliencePipelineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ResiliencePipelineOptions();
        configure(options);
        return AddDefinition(services, options.Freeze(name));
    }

    private static IServiceCollection AddDefinition<T>(IServiceCollection services, ResiliencePipelineDefinition<T> definition)
    {
        services.AddBingResilience();
        if (services.Any(d => d.ImplementationInstance is IPipelineRegistration r && r.Identity == definition.Identity))
            throw new ResilienceConfigurationException($"管线 '{definition.Identity}' 重复注册。");
        services.AddSingleton<IPipelineRegistration>(new PipelineRegistration<T>(definition));
        return services;
    }
}
internal interface IPipelineRegistration
{
    ResiliencePipelineIdentity Identity { get; }
    string? ProviderName { get; }
    ResilienceCapabilities Capabilities { get; }
    string Group { get; }
    object CreateHandle(ResilienceRegistry registry);
    IPipelineRegistration WithPartition(string partition);
    TimeSpan MinimumRetention { get; }
    object Create(IResilienceProvider provider, ResiliencePipelineBuildContext context);
}
internal sealed class PipelineRegistration<T>(ResiliencePipelineDefinition<T> definition) : IPipelineRegistration
{
    public ResiliencePipelineIdentity Identity => definition.Identity;
    public string? ProviderName => definition.ProviderName;
    public ResilienceCapabilities Capabilities => definition.RequiredCapabilities;
    public string Group => definition.SwitchGroup ?? (definition.Identity.Purpose.StartsWith("http.", StringComparison.Ordinal) ? "http:" : "generic:") + definition.Identity.Name;
    public object CreateHandle(ResilienceRegistry registry) => new StablePipeline<T>(registry, definition.Identity, Group);
    public IPipelineRegistration WithPartition(string partition) => new PipelineRegistration<T>(definition with { Identity = definition.Identity with { Partition = partition } });
    public TimeSpan MinimumRetention => definition.CircuitBreaker.Enabled ? (definition.CircuitBreaker.SamplingDuration > definition.CircuitBreaker.BreakDuration ? definition.CircuitBreaker.SamplingDuration : definition.CircuitBreaker.BreakDuration) : TimeSpan.Zero;
    public object Create(IResilienceProvider provider, ResiliencePipelineBuildContext context) =>
        new ObservedPipeline<T>(provider.CreatePipeline(definition, context), context.Identity, provider.Name);
}
internal sealed class ResilienceStartupValidator(IResiliencePipelineProvider pipelines) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) { pipelines.Validate(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
