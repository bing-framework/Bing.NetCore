namespace Bing.Locks;

/// <summary>
/// 业务锁
/// </summary>
public interface ILock
{
    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <returns>true:成功锁定; false:之前已被锁定</returns>
    bool LockTake(string key, string value, TimeSpan expiration);

    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <returns>true:成功锁定; false:之前已被锁定</returns>
    Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration);

    /// <summary>
    /// 释放一个锁
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <returns>true:释放成功; false:释放失败</returns>
    bool LockRelease(string key, string value);

    /// <summary>
    /// 释放一个锁
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <returns>true:释放成功; false:释放失败</returns>
    Task<bool> LockReleaseAsync(string key, string value);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    void ExecuteWithLock(string key, string value, TimeSpan expiration, Action executeAction);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    Task ExecuteWithLockAsync(string key, string value, TimeSpan expiration, Func<Task> executeAction);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <typeparam name="T">返回对象类型</typeparam>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    /// <param name="defaultValue">默认值</param>
    T ExecuteWithLock<T>(string key, string value, TimeSpan expiration, Func<T> executeAction, T defaultValue = default);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <typeparam name="T">返回对象类型</typeparam>
    /// <param name="key">锁定标识</param>
    /// <param name="value">当前占用值</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    /// <param name="defaultValue">默认值</param>
    Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction, T defaultValue = default);
}
