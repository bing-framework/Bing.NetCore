namespace Bing.Threading;

/// <summary>
/// 定义按当前执行上下文保存和读取环境数据的能力。
/// </summary>
/// <remarks>
/// 用于在同一逻辑异步调用链中传递上下文信息，避免逐层显式传递参数。具体实现决定数据的传播和隔离方式。
/// </remarks>
public interface IAmbientDataContext
{
    /// <summary>
    /// 在当前执行上下文中设置指定键的数据。
    /// </summary>
    /// <param name="key">用于定位上下文数据的稳定键。</param>
    /// <param name="value">要保存的对象值；可为 <c>null</c>。</param>
    void SetData(string key, object value);

    /// <summary>
    /// 从当前执行上下文中获取指定键的数据。
    /// </summary>
    /// <param name="key">用于定位上下文数据的稳定键。</param>
    /// <returns>当前执行上下文中的对象值；未设置时返回 <c>null</c>。</returns>
    object GetData(string key);
}
