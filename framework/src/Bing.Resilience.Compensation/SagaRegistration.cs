using Bing.Resilience.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace Bing.Resilience.Compensation;

public static class SagaRegistrationExtensions
{
    public static IServiceCollection AddBingSaga(this IServiceCollection services, Action<SagaOptions> configure, params SagaHandlerRegistration[] handlers)
    {
        if (services.Any(x => x.ServiceType == typeof(SagaCatalog))) throw new ResilienceConfigurationException("Saga 已登记。");
        var value = new SagaOptions(); configure(value);
        value.Retention?.Validate();
        if (value.Authorize is null || value.MaxAttemptsPerStep is < 1 or > 10000 || value.MaxParallelCompensations is < 1 or > 32 ||
            value.LeaseDuration < TimeSpan.FromMilliseconds(300) || value.LeaseDuration > TimeSpan.FromHours(1) ||
            value.AttemptTimeout <= TimeSpan.Zero || value.AttemptTimeout > TimeSpan.FromHours(1) ||
            value.RetryDelay < TimeSpan.Zero || value.RetryDelay > TimeSpan.FromDays(1) ||
            value.PollInterval < TimeSpan.FromMilliseconds(10) || value.MaximumLifetime <= TimeSpan.Zero || value.MaximumLifetime > TimeSpan.FromDays(30))
            throw new ResilienceConfigurationException("Saga 预算、授权或并行限制无效。");
        services.AddSingleton(new SagaOptions { Authorize = value.Authorize, MaxAttemptsPerStep = value.MaxAttemptsPerStep,
            MaxParallelCompensations = value.MaxParallelCompensations, LeaseDuration = value.LeaseDuration, AttemptTimeout = value.AttemptTimeout,
            RetryDelay = value.RetryDelay, PollInterval = value.PollInterval, MaximumLifetime = value.MaximumLifetime, StartWorker = value.StartWorker, Retention = value.Retention });
        services.AddSingleton(new SagaCatalog(handlers));
        services.AddSingleton<ISagaCoordinator, SagaCoordinator>();
        services.AddSingleton(sp => new SagaWorker(sp.GetRequiredService<IResilienceOperationStore>(), sp.GetRequiredService<IOperationDataProtector>(),
            sp.GetRequiredService<SagaCatalog>(), sp.GetRequiredService<SagaOptions>(), sp.GetRequiredService<ResilienceOperationCatalog>(),
            sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), sp.GetService<ILogger<SagaWorker>>() ?? NullLogger<SagaWorker>.Instance));
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SagaWorker>());
        return services;
    }
}
internal sealed class SagaCatalog
{
    private readonly Dictionary<(string, string), SagaHandlerRegistration> _handlers;
    internal SagaCatalog(IEnumerable<SagaHandlerRegistration> handlers)
    {
        try { _handlers = handlers.ToDictionary(x => (x.Name, x.Version)); }
        catch (ArgumentException) { throw new ResilienceConfigurationException("Saga 处理器名称版本重复。"); }
        if (_handlers.Count == 0 || _handlers.Values.Any(x => string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Version) || x.Factory is null))
            throw new ResilienceConfigurationException("Saga 处理器登记无效。");
    }
    internal SagaHandlerRegistration Get(SagaStepDefinition step) => _handlers.TryGetValue((step.Handler, step.HandlerVersion), out var handler)
        ? handler : throw new ResilienceConfigurationException("缺少 Saga 历史处理器版本。");
}
