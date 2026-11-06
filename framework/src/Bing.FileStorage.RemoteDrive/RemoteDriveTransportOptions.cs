namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 网盘传输的响应与空闲超时配置。
/// </summary>
/// <remarks>数据传输的无进展期限；持续传输没有固定的总耗时上限。</remarks>
public sealed class RemoteDriveTransportOptions
{
    /// <summary>
    /// 获取或设置连接和响应头等待上限。
    /// </summary>
    /// <remarks>上传正文发送完毕后重新计时。默认值为 45 秒。</remarks>
    public TimeSpan ResponseHeadersTimeout { get; set; } = TimeSpan.FromSeconds(45);
    /// <summary>
    /// 获取或设置读写没有产生字节进展的最长时间。
    /// </summary>
    /// <remarks>默认值为 2 分钟。</remarks>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 验证并复制远程传输超时选项。
    /// </summary>
    /// <returns>包含响应头超时和空闲超时的新选项实例。</returns>
    /// <remarks>
    /// 两种超时必须大于零且不超过一天。
    /// </remarks>
    internal RemoteDriveTransportOptions Snapshot()
    {
        if (ResponseHeadersTimeout <= TimeSpan.Zero || ResponseHeadersTimeout > TimeSpan.FromDays(1) ||
            IdleTimeout <= TimeSpan.Zero || IdleTimeout > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(RemoteDriveTransportOptions), "传输期限必须大于零且不超过一天。");
        return new() { ResponseHeadersTimeout = ResponseHeadersTimeout, IdleTimeout = IdleTimeout };
    }
}
