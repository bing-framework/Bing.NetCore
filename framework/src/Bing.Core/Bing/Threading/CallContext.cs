using System.Collections.Concurrent;

namespace Bing.Threading;

/// <summary>
/// 调用上下文
/// </summary>
public static class CallContext
{
    /// <summary>
    /// 状态字典
    /// </summary>
    private static readonly ConcurrentDictionary<string, AsyncLocal<object>> _state = new ConcurrentDictionary<string, AsyncLocal<object>>();

    /// <summary>
    /// 设置值
    /// </summary>
    /// <param name="name">键名</param>
    /// <param name="data">数据</param>
    public static void SetValue(string name, object data) => _state.GetOrAdd(name, _ => new AsyncLocal<object>()).Value = data;

    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="name">键名</param>
    /// <returns>如果指定键名不存在，则返回 null。</returns>
    public static object GetValue(string name) => _state.TryGetValue(name, out var data) ? data.Value : null;

    /// <summary>
    /// 移除
    /// </summary>
    /// <param name="name">键名</param>
    public static void Remove(string name) => _state.TryRemove(name, out _);

    /// <summary>
    /// 清空
    /// </summary>
    public static void Clear() => _state.Clear();
}
