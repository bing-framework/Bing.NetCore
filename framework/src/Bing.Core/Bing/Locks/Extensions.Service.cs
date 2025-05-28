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
    public static void AddLocalLock(this IServiceCollection services) => services.TryAddSingleton<ILock, LocalLock>();

    /// <summary>
    /// 注册业务锁（本地）
    /// </summary>
    /// <typeparam name="TLocalLock">本地锁实现类型</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddLocalLock<TLocalLock>(this IServiceCollection services) where TLocalLock : class, ILock => services.TryAddSingleton<ILock, TLocalLock>();
}
