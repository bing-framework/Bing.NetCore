using System.Collections.Concurrent;
using Bing.DependencyInjection;

namespace Bing.Threading;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 的环境数据上下文
/// </summary>
public class AsyncLocalAmbientDataContext : IAmbientDataContext, ISingletonDependency
{
    /// <summary>
    /// 异步线程本地存储字典
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly ConcurrentDictionary<string, AsyncLocal<object>> AsyncLocalDictionary = new();

    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="key">键名</param>
    /// <param name="value">对象值</param>
    public void SetData(string key, object value)
    {
        var asyncLocal = AsyncLocalDictionary.GetOrAdd(key, (k) => new AsyncLocal<object>());
        asyncLocal.Value = value;
    }

    /// <summary>
    /// 获取数据
    /// </summary>
    /// <param name="key">键名</param>
    /// <returns>对象值</returns>
    public object GetData(string key)
    {
        var asyncLocal = AsyncLocalDictionary.GetOrAdd(key, (k) => new AsyncLocal<object>());
        return asyncLocal.Value;
    }
}
