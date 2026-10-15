using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Serialization;

namespace Bing.Events.Cap;

/// <summary>
/// 将原生订阅调用逃出的取消异常转换为 CAP 可识别的消费失败。
/// </summary>
/// <remarks>
/// CAP 8.0.1 执行器会吞掉直接的取消异常；本适配器保留原异常供失败诊断。
/// 需要显式启用并通过 ISubscribeInvoker 调用，不自行重发消息或调度重试。
/// </remarks>
public class BingSubscribeInvoker : SubscribeInvoker, ISubscribeInvoker, IBingCapConsumerRuntime
{
    /// <summary>
    /// 排空状态和在途回调数量的同步锁。
    /// </summary>
    private readonly object _drainGate = new();
    /// <summary>
    /// 当前是否已经请求排空。
    /// </summary>
    private bool _draining;
    /// <summary>
    /// 当前仍在执行的回调数量。
    /// </summary>
    private int _inFlight;
    /// <summary>
    /// 排空完成通知。
    /// </summary>
    private TaskCompletionSource<bool> _drained;

    /// <summary>
    /// 初始化一个 <see cref="BingSubscribeInvoker" /> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">传递给原生调用器的宿主服务提供程序。</param>
    /// <param name="serializer">传递给原生调用器的 CAP 序列化器。</param>
    public BingSubscribeInvoker(IServiceProvider serviceProvider, ISerializer serializer)
        : base(serviceProvider, serializer)
    {
    }

    /// <inheritdoc />
    public bool IsDraining
    {
        get
        {
            lock (_drainGate)
                return _draining;
        }
    }

    /// <inheritdoc />
    public int InFlightCount
    {
        get
        {
            lock (_drainGate)
                return _inFlight;
        }
    }

    /// <inheritdoc />
    public void BeginDrain()
    {
        lock (_drainGate)
        {
            if (_draining)
                return;

            _draining = true;
            if (_inFlight == 0)
                _drained = CreateCompletedSource();
            else
                _drained ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <inheritdoc />
    public async Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        Task drained;
        lock (_drainGate)
        {
            if (!_draining)
                return false;
            if (_inFlight == 0)
                return true;
            drained = _drained?.Task ?? throw new InvalidOperationException("CAP 消费排空状态不完整。");
        }

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var completed = await Task.WhenAny(drained,
            timeout == Timeout.InfiniteTimeSpan
                ? Task.Delay(Timeout.InfiniteTimeSpan, waitCancellation.Token)
                : Task.Delay(timeout, waitCancellation.Token)).ConfigureAwait(false);
        waitCancellation.Cancel();
        return ReferenceEquals(completed, drained) && drained.IsCompletedSuccessfully;
    }

    /// <inheritdoc />
    async Task<ConsumerExecutedResult> ISubscribeInvoker.InvokeAsync(ConsumerContext context,
        CancellationToken cancellationToken)
    {
        EnterInvocation();
        try
        {
            return await base.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            var failure = new InvalidOperationException("CAP 消费者取消，交由持久化失败重试处理。", exception);
            failure.Data["Bing.CapConsumerCancellation"] = true;
            throw failure;
        }
        finally
        {
            ExitInvocation();
        }
    }

    /// <summary>
    /// 登记一个新的 CAP 消费回调。
    /// </summary>
    private void EnterInvocation()
    {
        lock (_drainGate)
        {
            if (_draining)
            {
                var exception = new InvalidOperationException("CAP 消费正在排空，暂不接收新的消费回调。");
                exception.Data["Bing.CapConsumerDraining"] = true;
                throw exception;
            }

            _inFlight++;
        }
    }

    /// <summary>
    /// 登记一个 CAP 消费回调已经退出。
    /// </summary>
    private void ExitInvocation()
    {
        lock (_drainGate)
        {
            _inFlight--;
            if (_draining && _inFlight == 0)
                (_drained ??= CreateCompletedSource()).TrySetResult(true);
        }
    }

    /// <summary>
    /// 创建已经完成的排空通知。
    /// </summary>
    private static TaskCompletionSource<bool> CreateCompletedSource()
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.TrySetResult(true);
        return source;
    }
}
