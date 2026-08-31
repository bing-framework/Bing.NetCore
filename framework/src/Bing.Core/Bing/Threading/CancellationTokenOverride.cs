namespace Bing.Threading;

/// <summary>
/// 表示在环境范围中保存的不可变取消令牌覆盖值。
/// </summary>
public class CancellationTokenOverride
{
    /// <summary>
    /// 获取当前范围要使用的取消令牌。
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 使用取消令牌初始化 <see cref="CancellationTokenOverride"/> 的实例。
    /// </summary>
    /// <param name="cancellationToken">当前范围要提供的取消令牌。</param>
    public CancellationTokenOverride(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }
}
