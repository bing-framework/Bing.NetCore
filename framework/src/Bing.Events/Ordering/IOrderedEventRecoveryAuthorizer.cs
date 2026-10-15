using Bing.Data.Events.Ordering;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Events.Ordering;

/// <summary>
/// 检查有序事件人工恢复权限。
/// </summary>
/// <remarks>授权器在开启恢复事务前执行；多个授权器均必须通过。</remarks>
public interface IOrderedEventRecoveryAuthorizer
{
    /// <summary>
    /// 检查指定消费者和业务流是否允许恢复。
    /// </summary>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="stream">业务流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AuthorizeAsync(string consumer, OrderedEventStream stream, CancellationToken cancellationToken = default);
}

/// <summary>
/// 注册有序事件人工恢复授权器。
/// </summary>
public static class OrderedEventRecoveryAuthorizationExtensions
{
    /// <summary>
    /// 注册有序事件人工恢复授权器。
    /// </summary>
    /// <typeparam name="TAuthorizer">授权器类型。</typeparam>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddBingOrderedEventRecoveryAuthorization<TAuthorizer>(
        this IServiceCollection services)
        where TAuthorizer : class, IOrderedEventRecoveryAuthorizer
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddScoped<IOrderedEventRecoveryAuthorizer, TAuthorizer>();
        return services;
    }
}
