using System.Collections.Concurrent;
using Bing.DependencyInjection;

namespace Bing.Threading;

/// <summary>
/// 基于 <see cref="AsyncLocal{T}"/> 的环境数据上下文实现。
/// </summary>
public class AsyncLocalAmbientDataContext : IAmbientDataContext, ISingletonDependency
{
    /// <summary>
    /// 保存上下文键与其共享 <see cref="AsyncLocal{T}"/> 槽位的映射。
    /// </summary>
    /// <remarks>键槽会保留在进程内；调用方应使用稳定且有限的键集合。字典仅保护槽位映射，不保证保存对象的线程安全。</remarks>
    // ReSharper disable once InconsistentNaming
    private static readonly ConcurrentDictionary<string, AsyncLocal<object>> AsyncLocalDictionary = new();

    /// <inheritdoc />
    /// <remarks>值随默认 <see cref="ExecutionContext"/> 流动规则传播；不同异步执行流中的值相互隔离。</remarks>
    public void SetData(string key, object value)
    {
        var asyncLocal = AsyncLocalDictionary.GetOrAdd(key, (k) => new AsyncLocal<object>());
        asyncLocal.Value = value;
    }

    /// <inheritdoc />
    /// <remarks>即使当前执行流未设置值，也会为该键创建并保留 <see cref="AsyncLocal{T}"/> 槽位。</remarks>
    public object GetData(string key)
    {
        var asyncLocal = AsyncLocalDictionary.GetOrAdd(key, (k) => new AsyncLocal<object>());
        return asyncLocal.Value;
    }
}
