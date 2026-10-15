using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Locks;

/// <summary>
/// 业务锁 服务扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册业务锁（本地）
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddLocalLock(this IServiceCollection services)
    {
        services.TryAddSingleton<ILock, LocalLock>();
        TryAddLeaseProvider(services);
    }

    /// <summary>
    /// 注册业务锁（本地）
    /// </summary>
    /// <typeparam name="TLocalLock">本地锁实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddLocalLock<TLocalLock>(this IServiceCollection services)
        where TLocalLock : class, ILock
    {
        services.TryAddSingleton<ILock, TLocalLock>();
        if (typeof(ILockLeaseProvider).IsAssignableFrom(typeof(TLocalLock)))
            services.TryAddSingleton<ILockLeaseProvider>(provider =>
                (ILockLeaseProvider)provider.GetRequiredService<ILock>());
    }

    /// <summary>
    /// 在当前锁实现明确支持租约时注册租约服务。
    /// </summary>
    private static void TryAddLeaseProvider(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(item => item.ServiceType == typeof(ILock));
        if (descriptor?.ImplementationFactory != null)
            return;
        if (descriptor?.ImplementationInstance != null && descriptor.ImplementationInstance is not ILockLeaseProvider)
            return;
        if (descriptor?.ImplementationType != null && !typeof(ILockLeaseProvider).IsAssignableFrom(descriptor.ImplementationType))
            return;

        services.TryAddSingleton<ILockLeaseProvider>(provider =>
            (ILockLeaseProvider)provider.GetRequiredService<ILock>());
    }
}
