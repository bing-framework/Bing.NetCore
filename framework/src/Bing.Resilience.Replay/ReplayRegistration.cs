using Bing.Resilience.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Resilience.Replay;

public static class ReplayRegistrationExtensions
{
    public static IServiceCollection AddBingDurableReplay(this IServiceCollection services, Action<ReplayOptions> configure,
        params ReplayHandlerRegistration[] handlers)
    {
        if (services.Any(x => x.ServiceType == typeof(ReplayCatalog))) throw new ResilienceConfigurationException("持久重放已经登记。");
        var options = new ReplayOptions(); configure(options);
        options.Retention?.Validate();
        if (options.Authorize is null || options.MaxAttempts is < 1 or > 10000 || options.LeaseDuration < TimeSpan.FromMilliseconds(300) ||
            options.LeaseDuration > TimeSpan.FromHours(1) || options.AttemptTimeout <= TimeSpan.Zero || options.AttemptTimeout > TimeSpan.FromHours(1) ||
            options.RetryDelay < TimeSpan.Zero || options.RetryDelay > TimeSpan.FromDays(1) ||
            options.PollInterval < TimeSpan.FromMilliseconds(10) || options.MaximumLifetime <= TimeSpan.Zero || options.MaximumLifetime > TimeSpan.FromDays(30))
            throw new ResilienceConfigurationException("持久重放预算、尝试次数或授权配置无效。");
        var frozen = new ReplayOptions { Authorize = options.Authorize, MaxAttempts = options.MaxAttempts, LeaseDuration = options.LeaseDuration,
            AttemptTimeout = options.AttemptTimeout, RetryDelay = options.RetryDelay, PollInterval = options.PollInterval,
            MaximumLifetime = options.MaximumLifetime, StartWorker = options.StartWorker, Retention = options.Retention };
        services.AddSingleton(new ReplayCatalog(handlers));
        services.AddSingleton(frozen);
        services.AddSingleton<ReplayCoordinator>();
        services.AddSingleton<IReplayCoordinator>(sp => sp.GetRequiredService<ReplayCoordinator>());
        services.AddSingleton(sp => new ReplayWorker(sp.GetRequiredService<IResilienceOperationStore>(), sp.GetRequiredService<IOperationDataProtector>(),
            sp.GetRequiredService<ReplayCatalog>(), sp.GetRequiredService<ReplayOptions>(), sp.GetRequiredService<ResilienceOperationCatalog>(),
            sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), sp.GetService<Microsoft.Extensions.Logging.ILogger<ReplayWorker>>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ReplayWorker>.Instance));
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ReplayWorker>());
        return services;
    }
}
internal sealed class ReplayCatalog
{
    private readonly Dictionary<(string, string), ReplayHandlerRegistration> _handlers;
    internal ReplayCatalog(IEnumerable<ReplayHandlerRegistration> handlers)
    {
        try { _handlers = handlers.ToDictionary(x => (x.Name, x.SchemaVersion)); }
        catch (ArgumentException) { throw new ResilienceConfigurationException("重放处理器名称与版本重复。"); }
        if (_handlers.Count == 0 || _handlers.Values.Any(x => string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.SchemaVersion) ||
            string.IsNullOrWhiteSpace(x.OperationName) || x.Factory is null)) throw new ResilienceConfigurationException("重放处理器登记无效。");
    }
    internal ReplayHandlerRegistration Get(ReplayCommand command) =>
        _handlers.TryGetValue((command.CommandType, command.SchemaVersion), out var value) && value.OperationName == command.OperationName
            ? value : throw new ResilienceConfigurationException("缺少匹配的历史重放处理器。");
}
