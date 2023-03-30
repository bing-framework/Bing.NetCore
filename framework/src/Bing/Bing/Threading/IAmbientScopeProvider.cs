namespace Bing.Threading;

/// <summary>
/// 环境范围提供程序
/// </summary>
/// <typeparam name="T">泛型类型</typeparam>
public interface IAmbientScopeProvider<T>
{
    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="contextKey">上下文键名</param>
    /// <returns>对象值</returns>
    T GetValue(string contextKey);

    /// <summary>
    /// 开始范围
    /// </summary>
    /// <param name="contextKey">上下文键名</param>
    /// <param name="value">对象值</param>
    IDisposable BeginScope(string contextKey, T value);
}
