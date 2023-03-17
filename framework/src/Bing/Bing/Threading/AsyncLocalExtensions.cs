namespace Bing.Threading;

/// <summary>
/// <see cref="AsyncLocal{T}"/> 扩展
/// </summary>
public static class AsyncLocalExtensions
{
    /// <summary>
    /// 设置范围
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="asyncLocal">异步本地存储</param>
    /// <param name="value">值</param>
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
