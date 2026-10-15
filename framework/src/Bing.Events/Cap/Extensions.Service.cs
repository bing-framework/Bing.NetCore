using Bing.Events.Default;
using Bing.Events.Handlers;
using Bing.Events.Messages;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bing.Events.Cap;

/// <summary>
/// 事件总线扩展
/// </summary>
public static class Extensions
{
    /// <summary>
    /// 注册CAP事件总线服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="action">配置操作</param>
    /// <param name="builderSetup">CAP构建器配置操作</param>
    /// <returns>完成 CAP 事件总线注册后的服务集合。</returns>
    /// <remarks>宿主等待 CAP 启动流程结束后再执行停止；停止超时不代表后台清理已经完成。</remarks>
    public static IServiceCollection AddCapEventBus(this IServiceCollection services, Action<CapOptions> action, Action<CapBuilder> builderSetup = null)
    {
        services.TryAddSingleton<IEventHandlerManager, EventHandlerManager>();
        services.TryAddSingleton<ISimpleEventBus, Default.EventBus>();
        services.TryAddScoped<IMessageEventBus, MessageEventBus>();
        services.TryAddScoped<IEventBus, EventBus>();
        var builder = services.AddCap(action);
        ConfigureHostedLifecycle(services);
        builderSetup?.Invoke(builder);
        return services;
    }

    /// <summary>
    /// 启用 CAP 消费取消的失败重试适配。
    /// </summary>
    /// <param name="services">当前宿主的服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 显式启用后，未被消费过滤器处理的取消异常进入 CAP 失败路径。
    /// 自定义执行器应继承 BingSubscribeInvoker；未知执行器不会被覆盖。
    /// 工厂注册可以直接返回该执行器，不需要调用本方法。
    /// </remarks>
    public static IServiceCollection EnableBingCapConsumerCancellationRetry(this IServiceCollection services)
        => EnableBingCapConsumerCancellationRetry(services, null);

    /// <summary>
    /// 启用 CAP 消费取消的失败重试适配并配置宿主排空。
    /// </summary>
    /// <param name="services">当前宿主的服务集合。</param>
    /// <param name="configure">消费宿主配置。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// <paramref name="configure"/> 只影响由 Bing 接管的宿主停止排空；手工使用
    /// <see cref="IBingCapConsumerRuntime"/> 时仍应直接传入等待超时。
    /// </remarks>
    public static IServiceCollection EnableBingCapConsumerCancellationRetry(this IServiceCollection services,
        Action<BingCapConsumerOptions> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddOptions<BingCapConsumerOptions>();
        if (configure != null)
            services.Configure(configure);
        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType != typeof(ISubscribeInvoker))
                continue;
            if (descriptor.ImplementationInstance is BingSubscribeInvoker ||
                descriptor.ImplementationType != null &&
                typeof(BingSubscribeInvoker).IsAssignableFrom(descriptor.ImplementationType))
            {
                RegisterConsumerRuntime(services, descriptor.Lifetime);
                RegisterHostDrain(services, descriptor.Lifetime);
                return services;
            }
            if (descriptor.ImplementationType != typeof(SubscribeInvoker))
                throw new InvalidOperationException("当前 CAP 执行器为自定义注册，请继承 BingSubscribeInvoker 后保留原注册。");
            services[index] = ServiceDescriptor.Singleton<ISubscribeInvoker, BingSubscribeInvoker>();
            RegisterConsumerRuntime(services, ServiceLifetime.Singleton);
            RegisterHostDrain(services, ServiceLifetime.Singleton);
            return services;
        }
        services.AddSingleton<ISubscribeInvoker, BingSubscribeInvoker>();
        RegisterConsumerRuntime(services, ServiceLifetime.Singleton);
        RegisterHostDrain(services, ServiceLifetime.Singleton);
        return services;
    }

    /// <summary>
    /// 注册与 CAP 调用器关联的消费排空运行时。
    /// </summary>
    private static void RegisterConsumerRuntime(IServiceCollection services, ServiceLifetime lifetime)
    {
        services.TryAdd(ServiceDescriptor.Describe(typeof(IBingCapConsumerRuntime), provider =>
            provider.GetRequiredService<ISubscribeInvoker>() as IBingCapConsumerRuntime ??
            throw new InvalidOperationException("当前 CAP 执行器未实现 Bing 消费排空契约。"), lifetime));
    }

    /// <summary>
    /// 仅为单例调用器注册宿主停止排空适配器。
    /// </summary>
    private static void RegisterHostDrain(IServiceCollection services, ServiceLifetime lifetime)
    {
        if (lifetime == ServiceLifetime.Singleton)
            services.TryAddSingleton<BingCapConsumerHostDrain>();
    }

    /// <summary>
    /// 替换原生 CAP 宿主启动回调。
    /// </summary>
    /// <remarks>只调整 CAP 自身的 HostedService，其他后台服务保持原注册顺序。</remarks>
    private static void ConfigureHostedLifecycle(IServiceCollection services)
    {
        for (var index = 0; index < services.Count; index++)
        {
            var descriptor = services[index];
            var resultType = descriptor.ImplementationFactory?.Method.ReturnType;
            if (descriptor.ServiceType != typeof(IHostedService) || resultType == null ||
                resultType.Assembly != typeof(IBootstrapper).Assembly ||
                !typeof(IBootstrapper).IsAssignableFrom(resultType))
                continue;

            services.TryAddSingleton<CapHostedLifecycleService>();
            if (services.Any(item => item.ServiceType == typeof(IHostedService) &&
                    item.ImplementationFactory?.Method.ReturnType == typeof(CapHostedLifecycleService)))
            {
                services.RemoveAt(index--);
            }
            else
            {
                services[index] = ServiceDescriptor.Singleton<IHostedService, CapHostedLifecycleService>(provider =>
                    provider.GetRequiredService<CapHostedLifecycleService>());
            }
        }
    }
}
