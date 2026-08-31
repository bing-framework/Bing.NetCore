namespace Bing.Threading;

/// <summary>
/// 提供 <see cref="AsyncLocal{T}"/> 的临时值覆盖扩展。
/// </summary>
public static class AsyncLocalExtensions
{
    /// <summary>
    /// 在当前执行上下文中临时设置异步本地值。
    /// </summary>
    /// <typeparam name="T">异步本地值的类型。</typeparam>
    /// <param name="asyncLocal">要临时覆盖的异步本地槽位。</param>
    /// <param name="value">在当前范围内使用的值。</param>
    /// <returns>释放后恢复调用时捕获值的作用域对象。</returns>
    /// <remarks>应在同一逻辑异步流中按后进先出顺序释放；乱序或跨执行上下文释放可能覆盖更内层的值。</remarks>
    public static IDisposable SetScoped<T>(this AsyncLocal<T> asyncLocal, T value)
    {
        var previousValue = asyncLocal.Value;
        asyncLocal.Value = value;
        return new DisposeAction(() =>
        {
            asyncLocal.Value = previousValue;
        });
    }
}
