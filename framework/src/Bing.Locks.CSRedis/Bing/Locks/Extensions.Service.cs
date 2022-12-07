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
    public static void AddRedisDistributedLock(this IServiceCollection services) => services.TryAddSingleton<IDistributedLock, CSRedisDistributedLock>();

    /// <summary>
    /// 注册业务锁（分布式）
    /// </summary>
    /// <typeparam name="TDistributedLock">分布式锁实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddRedisDistributedLock<TDistributedLock>(this IServiceCollection services) where TDistributedLock : class, IDistributedLock => services.TryAddSingleton<IDistributedLock, TDistributedLock>();
}