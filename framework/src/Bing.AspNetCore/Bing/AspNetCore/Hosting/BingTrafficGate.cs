using System.Threading;

namespace Bing.AspNetCore.Hosting;

/// <summary>
/// 使用原子状态和引用计数管理 HTTP 在途请求。
/// </summary>
public sealed class BingTrafficGate : IBingTrafficGate
{
    /// <summary>
    /// 尚未完成预热的状态值。
    /// </summary>
    private const int Starting = 0;
    /// <summary>
    /// 允许接收请求的状态值。
    /// </summary>
    private const int Accepting = 1;
    /// <summary>
    /// 拒绝新请求并等待在途请求结束的终态值。
    /// </summary>
    private const int Draining = 2;

    /// <summary>
    /// 状态：0 未就绪，1 接收中，2 排空中。
    /// </summary>
    private int _state = Accepting;

    /// <summary>
    /// 在途请求数。
    /// </summary>
    private int _inFlight;

    /// <summary>
    /// 排空完成通知。
    /// </summary>
    private TaskCompletionSource<bool> _drainCompletion;

    /// <summary>
    /// 初始化一个 <see cref="BingTrafficGate" /> 类型的实例。
    /// </summary>
    /// <remarks>默认立即接收请求。</remarks>
    public BingTrafficGate()
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="BingTrafficGate" /> 类型的实例。
    /// </summary>
    /// <param name="options">宿主就绪配置。</param>
    public BingTrafficGate(BingTrafficGateOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (!options.StartReady)
            _state = Starting;
    }

    /// <inheritdoc />
    public bool IsReady => Volatile.Read(ref _state) == Accepting;

    /// <inheritdoc />
    public bool IsDraining => Volatile.Read(ref _state) == Draining;

    /// <inheritdoc />
    public int InFlightRequests => Math.Max(0, Volatile.Read(ref _inFlight));

    /// <inheritdoc />
    public void MarkReady()
    {
        Interlocked.CompareExchange(ref _state, Accepting, Starting);
    }

    /// <inheritdoc />
    public void BeginDrain()
    {
        if (Interlocked.CompareExchange(ref _state, Draining, Accepting) != Accepting)
            Interlocked.CompareExchange(ref _state, Draining, Starting);
        if (Volatile.Read(ref _inFlight) == 0)
            SignalDrainCompleted();
    }

    /// <inheritdoc />
    public bool TryEnter(out IDisposable lease)
    {
        lease = null;
        while (Volatile.Read(ref _state) == Accepting)
        {
            Interlocked.Increment(ref _inFlight);
            if (Volatile.Read(ref _state) == Accepting)
            {
                lease = new RequestLease(this);
                return true;
            }

            Release();
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (!IsDraining || InFlightRequests == 0)
            return true;

        var completion = LazyInitializer.EnsureInitialized(ref _drainCompletion,
            () => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        if (InFlightRequests == 0)
        {
            SignalDrainCompleted();
            return true;
        }

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var completed = await Task.WhenAny(completion.Task,
                timeout == Timeout.InfiniteTimeSpan ? Task.Delay(Timeout.Infinite, waitCancellation.Token) :
                    Task.Delay(timeout, waitCancellation.Token)).ConfigureAwait(false);
            return completed == completion.Task && completion.Task.IsCompletedSuccessfully;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            // 排空提前完成时取消剩余计时器，并解除调用方令牌的注册。
            waitCancellation.Cancel();
        }
    }

    /// <summary>
    /// 释放一个在途请求。
    /// </summary>
    private void Release()
    {
        if (Interlocked.Decrement(ref _inFlight) == 0 && IsDraining)
            SignalDrainCompleted();
    }

    /// <summary>
    /// 通知等待者排空完成。
    /// </summary>
    private void SignalDrainCompleted()
    {
        LazyInitializer.EnsureInitialized(ref _drainCompletion,
            () => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously))
            .TrySetResult(true);
    }

    /// <summary>
    /// 保证请求只释放一次的句柄。
    /// </summary>
    private sealed class RequestLease : IDisposable
    {
        /// <summary>
        /// 尚未释放的所属闸门；释放时原子置空。
        /// </summary>
        private BingTrafficGate _owner;

        /// <summary>
        /// 初始化一个 <see cref="RequestLease" /> 类型的实例。
        /// </summary>
        /// <param name="owner">记录当前请求的闸门。</param>
        public RequestLease(BingTrafficGate owner) => _owner = owner;

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
