using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Hosting;

/// <summary>
/// 注册 HTTP 流量闸门和宿主停止排空流程。
/// </summary>
public static class BingTrafficGateExtensions
{
    /// <summary>
    /// 注册流量闸门。
    /// </summary>
    public static IServiceCollection AddBingTrafficGate(this IServiceCollection services,
        Action<BingTrafficGateOptions> configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddOptions<BingTrafficGateOptions>();
        if (configure != null)
            services.Configure(configure);
        services.TryAddSingleton<IBingTrafficGate>(provider =>
            new BingTrafficGate(provider.GetRequiredService<IOptions<BingTrafficGateOptions>>().Value));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BingTrafficGateHostedService>());
        return services;
    }

    /// <summary>
    /// 将流量闸门加入请求管道。
    /// </summary>
    public static IApplicationBuilder UseBingTrafficGate(this IApplicationBuilder app) =>
        app?.UseMiddleware<BingTrafficGateMiddleware>() ?? throw new ArgumentNullException(nameof(app));

    /// <summary>
    /// 标记流量闸门已完成预热。
    /// </summary>
    public static IServiceProvider MarkBingTrafficReady(this IServiceProvider provider)
    {
        provider?.GetRequiredService<IBingTrafficGate>().MarkReady();
        return provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// 宿主停止时等待 HTTP 在途请求排空。
    /// </summary>
    private sealed class BingTrafficGateHostedService : IHostedService
    {
        /// <summary>
        /// 宿主的 HTTP 流量闸门。
        /// </summary>
        private readonly IBingTrafficGate _gate;

        /// <summary>
        /// 宿主排空等待配置。
        /// </summary>
        private readonly BingTrafficGateOptions _options;

        /// <summary>
        /// 排空未完成的诊断日志。
        /// </summary>
        private readonly ILogger<BingTrafficGateHostedService> _logger;

        /// <summary>
        /// 初始化一个 <see cref="BingTrafficGateHostedService" /> 类型的实例。
        /// </summary>
        public BingTrafficGateHostedService(IBingTrafficGate gate, IOptions<BingTrafficGateOptions> options,
            ILogger<BingTrafficGateHostedService> logger = null)
        {
            _gate = gate;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _gate.BeginDrain();
            if (await _gate.WaitForDrainAsync(_options.DrainTimeout, cancellationToken).ConfigureAwait(false))
                return;

            var inFlight = _gate.InFlightRequests;
            var cancelled = cancellationToken.IsCancellationRequested;
            _logger?.LogWarning("HTTP 排空未完成。InFlight={InFlight}, WaitCancelled={WaitCancelled}",
                inFlight, cancelled);
            Exception exception = cancelled
                ? new OperationCanceledException("HTTP 排空等待已取消，在途请求尚未完成。", cancellationToken)
                : new TimeoutException("HTTP 排空等待已超时，在途请求尚未完成。");
            exception.Data["Bing.TrafficGateInFlight"] = inFlight;
            throw exception;
        }
    }
}
