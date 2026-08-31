namespace Bing.Threading;

/// <summary>
/// 定义获取当前异步操作取消令牌的提供程序。
/// </summary>
public interface ICancellationTokenProvider
{
    /// <summary>
    /// 获取当前有效的取消令牌。
    /// </summary>
    /// <remarks>令牌可能由当前执行环境决定，调用方应在使用点读取而非长期缓存。</remarks>
    CancellationToken Token { get; }
}
