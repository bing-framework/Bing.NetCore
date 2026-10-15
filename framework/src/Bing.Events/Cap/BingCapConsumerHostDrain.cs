namespace Bing.Events.Cap;

/// <summary>
/// 为宿主停止流程转发单例 CAP 消费排空控制。
/// </summary>
/// <remarks>
/// 该类型只在单例 <see cref="IBingCapConsumerRuntime"/> 注册时创建，避免根容器捕获作用域调用器。
/// </remarks>
internal sealed class BingCapConsumerHostDrain
{
    /// <summary>
    /// 当前宿主使用的消费排空运行时。
    /// </summary>
    private readonly IBingCapConsumerRuntime _runtime;
    /// <summary>
    /// 当前宿主的排空等待配置。
    /// </summary>
    private readonly BingCapConsumerOptions _options;

    /// <summary>
    /// 初始化一个 <see cref="BingCapConsumerHostDrain" /> 类型的实例。
    /// </summary>
    /// <param name="runtime">宿主拥有的单例消费排空运行时。</param>
    /// <param name="options">宿主停止时使用的排空等待配置。</param>
    public BingCapConsumerHostDrain(IBingCapConsumerRuntime runtime,
        Microsoft.Extensions.Options.IOptions<BingCapConsumerOptions> options)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (_options.DrainTimeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(_options.DrainTimeout));
    }

    /// <summary>
    /// 请求停止新的消费回调。
    /// </summary>
    public void BeginDrain() => _runtime.BeginDrain();

    /// <summary>
    /// 等待在途消费回调实际退出。
    /// </summary>
    /// <returns>在等待期间完成排空时返回 <see langword="true"/>。</returns>
    public Task<bool> WaitForDrainAsync() =>
        _runtime.WaitForDrainAsync(_options.DrainTimeout, CancellationToken.None);
}
