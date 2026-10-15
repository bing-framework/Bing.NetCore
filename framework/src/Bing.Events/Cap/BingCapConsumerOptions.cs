using System;

namespace Bing.Events.Cap;

/// <summary>
/// 配置 CAP 消费宿主的排空等待行为。
/// </summary>
public sealed class BingCapConsumerOptions
{
    /// <summary>
    /// 初始化一个 <see cref="BingCapConsumerOptions" /> 类型的实例。
    /// </summary>
    public BingCapConsumerOptions()
    {
        DrainTimeout = Timeout.InfiniteTimeSpan;
    }

    /// <summary>
    /// 宿主停止时等待在途消费回调的最长时间。
    /// </summary>
    /// <remarks>
    /// 默认值为 <see cref="Timeout.InfiniteTimeSpan"/>。超时只报告排空未完成，
    /// 不会强制终止回调或释放仍在使用的作用域。
    /// </remarks>
    public TimeSpan DrainTimeout { get; set; }
}
