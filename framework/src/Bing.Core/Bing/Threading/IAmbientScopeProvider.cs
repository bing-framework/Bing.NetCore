namespace Bing.Threading;

/// <summary>
/// 定义在环境数据上下文中读取和临时覆盖值的作用域提供程序。
/// </summary>
/// <typeparam name="T">上下文值的类型。</typeparam>
public interface IAmbientScopeProvider<T>
{
    /// <summary>
    /// 获取当前环境范围中的值。
    /// </summary>
    /// <param name="contextKey">用于区分上下文值的键。</param>
    /// <returns>当前范围值；不存在时返回 <typeparamref name="T"/> 的默认值。</returns>
    T GetValue(string contextKey);

    /// <summary>
    /// 开始一个临时覆盖指定上下文值的环境范围。
    /// </summary>
    /// <param name="contextKey">用于区分上下文值的键。</param>
    /// <param name="value">在新范围中使用的值。</param>
    /// <returns>释放后恢复开始该范围前上下文状态的作用域对象。</returns>
    IDisposable BeginScope(string contextKey, T value);
}
