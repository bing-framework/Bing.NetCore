namespace Bing.Locks;

/// <summary>
/// 业务锁(<see cref="ILock"/>) 扩展
/// </summary>
public static class LockExtensions
{
    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <returns>true:成功锁定; false:之前已被锁定</returns>
    public static bool LockTak(this ILock @lock, string key, TimeSpan expiration) => @lock.LockTake(key, "1", expiration);

    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <returns>true:成功锁定; false:之前已被锁定</returns>
    public static async Task<bool> LockTakAsync(this ILock @lock, string key, TimeSpan expiration) =>
        await @lock.LockTakeAsync(key, "1", expiration);

    /// <summary>
    /// 释放一个锁
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <returns>true:释放成功; false:释放失败</returns>
    public static bool LockRelease(this ILock @lock, string key) => @lock.LockRelease(key, "1");

    /// <summary>
    /// 释放一个锁
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <returns>true:释放成功; false:释放失败</returns>
    public static async Task<bool> LockReleaseAsync(this ILock @lock, string key) => await @lock.LockReleaseAsync(key, "1");

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    public static void ExecuteWithLock(this ILock @lock, string key, TimeSpan expiration, Action executeAction) =>
        @lock.ExecuteWithLock(key, "1", expiration, executeAction);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    public static async Task ExecuteWithLockAsync(this ILock @lock, string key, TimeSpan expiration, Func<Task> executeAction) => 
        await @lock.ExecuteWithLockAsync(key, "1", expiration, executeAction);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <typeparam name="T">返回对象类型</typeparam>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    /// <param name="defaultValue">默认值</param>
    public static T ExecuteWithLock<T>(this ILock @lock, string key, TimeSpan expiration, Func<T> executeAction, T defaultValue = default) => 
        @lock.ExecuteWithLock(key, "1", expiration, executeAction, defaultValue);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <typeparam name="T">返回对象类型</typeparam>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    /// <param name="defaultValue">默认值</param>
    public static async Task<T> ExecuteWithLockAsync<T>(this ILock @lock, string key, TimeSpan expiration, Func<Task<T>> executeAction, T defaultValue = default) =>
        await @lock.ExecuteWithLockAsync(key, "1", expiration, executeAction, defaultValue);
}
