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
    /// 初始化一个 <see cref="CancellationTokenOverride"/> 类型的实例。
    /// </summary>
    /// <param name="cancellationToken">当前范围要提供的取消令牌。</param>
    public CancellationTokenOverride(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }
}
