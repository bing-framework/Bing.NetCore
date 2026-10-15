using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Locks;

/// <summary>
/// 业务锁 服务扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册业务锁（分布式）
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddRedisDistributedLock(this IServiceCollection services)
    {
        services.TryAddSingleton<IDistributedLock, CSRedisDistributedLock>();
        TryAddLeaseProvider(services);
    }

    /// <summary>
    /// 注册业务锁（分布式）
    /// </summary>
    /// <typeparam name="TDistributedLock">分布式锁实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddRedisDistributedLock<TDistributedLock>(this IServiceCollection services)
        where TDistributedLock : class, IDistributedLock
    {
        services.TryAddSingleton<IDistributedLock, TDistributedLock>();
        if (typeof(ILockLeaseProvider).IsAssignableFrom(typeof(TDistributedLock)))
            services.TryAddSingleton<ILockLeaseProvider>(provider =>
                (ILockLeaseProvider)provider.GetRequiredService<IDistributedLock>());
    }

    /// <summary>
    /// 在当前分布式锁实现明确支持租约时注册租约服务。
    /// </summary>
    private static void TryAddLeaseProvider(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(item => item.ServiceType == typeof(IDistributedLock));
        if (descriptor?.ImplementationFactory != null)
            return;
        if (descriptor?.ImplementationInstance != null && descriptor.ImplementationInstance is not ILockLeaseProvider)
            return;
        if (descriptor?.ImplementationType != null && !typeof(ILockLeaseProvider).IsAssignableFrom(descriptor.ImplementationType))
            return;

        services.TryAddSingleton<ILockLeaseProvider>(provider =>
            (ILockLeaseProvider)provider.GetRequiredService<IDistributedLock>());
    }
}
