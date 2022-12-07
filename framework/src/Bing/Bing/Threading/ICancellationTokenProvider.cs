namespace Bing.Threading;

/// <summary>
/// 异步任务取消令牌提供程序
/// </summary>
public interface ICancellationTokenProvider
{
    /// <summary>
    /// 异步任务取消令牌
    /// </summary>
    CancellationToken Token { get; }
}
