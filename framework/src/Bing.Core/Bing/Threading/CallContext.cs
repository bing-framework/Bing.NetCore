using System.Collections.Concurrent;

namespace Bing.Threading;

/// <summary>
/// 提供按当前执行上下文存取命名状态的静态入口。
/// </summary>
public static class CallContext
{
    /// <summary>
    /// 保存状态键与其共享 <see cref="AsyncLocal{T}"/> 槽位的映射。
    /// </summary>
    /// <remarks>键槽在调用 <see cref="SetValue"/> 后保留，保存对象本身的线程安全由调用方负责。</remarks>
    private static readonly ConcurrentDictionary<string, AsyncLocal<object>> _state = new ConcurrentDictionary<string, AsyncLocal<object>>();

    /// <summary>
    /// 在当前执行上下文中设置指定名称的状态值。
    /// </summary>
    /// <param name="name">状态键。</param>
    /// <param name="data">要保存的数据；可为 <c>null</c>。</param>
    public static void SetValue(string name, object data) => _state.GetOrAdd(name, _ => new AsyncLocal<object>()).Value = data;

    /// <summary>
    /// 获取当前执行上下文中指定名称的状态值。
    /// </summary>
    /// <param name="name">状态键。</param>
    /// <returns>当前执行上下文中的数据；键未注册或当前流未设置值时返回 <c>null</c>。</returns>
    public static object GetValue(string name) => _state.TryGetValue(name, out var data) ? data.Value : null;

    /// <summary>
    /// 移除指定名称的全局状态槽位映射。
    /// </summary>
    /// <param name="name">要移除的状态键。</param>
    /// <remarks>该操作移除键到 <see cref="AsyncLocal{T}"/> 的映射，而非仅清理当前执行上下文中的值。</remarks>
    public static void Remove(string name) => _state.TryRemove(name, out _);

    /// <summary>
    /// 移除所有全局状态槽位映射。
    /// </summary>
    /// <remarks>该操作清空键映射，而非逐一清理已捕获执行上下文中的值。</remarks>
    public static void Clear() => _state.Clear();
}
