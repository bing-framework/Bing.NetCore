namespace Bing.AspNetCore.Hosting;

/// <summary>
/// 控制 HTTP 请求接收、就绪和排空状态。
/// </summary>
public interface IBingTrafficGate
{
    /// <summary>
    /// 获取当前是否已准备接收请求。
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// 获取当前是否正在排空。
    /// </summary>
    bool IsDraining { get; }

    /// <summary>
    /// 获取当前在途请求数。
    /// </summary>
    int InFlightRequests { get; }

    /// <summary>
    /// 标记应用已完成预热并可以接收请求。
    /// </summary>
    void MarkReady();

    /// <summary>
    /// 停止接收新请求并开始排空。
    /// </summary>
    void BeginDrain();

    /// <summary>
    /// 尝试登记一个在途请求。
    /// </summary>
    /// <param name="lease">准入成功时用于释放在途请求登记的句柄。</param>
    /// <returns>允许登记本次请求时为 true；入口已关闭时为 false。</returns>
    bool TryEnter(out IDisposable lease);

    /// <summary>
    /// 等待当前在途请求排空。
    /// </summary>
    /// <returns>是否在取消或超时前完成排空。</returns>
    /// <param name="timeout">命令超时秒数；为空时沿用执行器配置。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
