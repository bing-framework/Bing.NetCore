namespace Bing.Threading;

/// <summary>
/// 取消令牌覆盖
/// </summary>
public class CancellationTokenOverride
{
    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 初始化一个<see cref="CancellationTokenOverride"/>类型的实例
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public CancellationTokenOverride(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }
}
