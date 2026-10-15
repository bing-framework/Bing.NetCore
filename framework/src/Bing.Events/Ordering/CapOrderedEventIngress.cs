using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Events.Ordering;

/// <summary>
/// 将 CAP 订阅收到的有序消息写入持久化收件箱。
/// </summary>
/// <remarks>
/// 该适配器不声明 CAP 主题。宿主仍需使用固定主题和稳定消费者组实现 <see cref="ICapSubscribe"/>
/// 订阅，并在方法内调用 <see cref="ReceiveAsync"/>；适配器完成入箱后订阅方法才可以返回确认。
/// </remarks>
public sealed class CapOrderedEventIngress
{
    /// <summary>
    /// 当前作用域的有序事件协调器。
    /// </summary>
    private readonly OrderedEventCoordinator _coordinator;

    /// <summary>
    /// 初始化一个 <see cref="CapOrderedEventIngress" /> 类型的实例。
    /// </summary>
    /// <param name="coordinator">负责持久化有序收件箱的协调器。</param>
    public CapOrderedEventIngress(OrderedEventCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>
    /// 将 CAP 消息写入指定逻辑消费者的有序收件箱。
    /// </summary>
    /// <param name="consumer">跨部署版本保持稳定的逻辑消费者名称。</param>
    /// <param name="headers">CAP 消息头。</param>
    /// <param name="payload">必须保持原始序列化形式的消息正文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示入箱事务完成的异步操作。</returns>
    /// <remarks>取消或入箱失败时应让 CAP 订阅方法抛出异常，以保留其原生重投语义。</remarks>
    public Task ReceiveAsync(string consumer, CapHeader headers, string payload,
        CancellationToken cancellationToken = default)
    {
        OrderedEventConsumerName.Validate(consumer, nameof(consumer));
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        return _coordinator.ReceiveAsync(consumer, headers, payload, cancellationToken);
    }
}

/// <summary>
/// 提供 CAP 有序入箱适配器的注册扩展。
/// </summary>
public static class CapOrderedEventIngressExtensions
{
    /// <summary>
    /// 注册当前作用域的 CAP 有序入箱适配器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>重复调用不会覆盖调用方已有的适配器实现。</remarks>
    public static IServiceCollection AddBingCapOrderedEventIngress(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.TryAddScoped<CapOrderedEventIngress>();
        return services;
    }
}
