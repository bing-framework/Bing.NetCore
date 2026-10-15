using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Admin.Jobs;

/// <summary>
/// 跟踪 Hangfire 任务并提供排空协调。
/// </summary>
public sealed class BingHangfireRuntime : IBingHangfireRuntime
{
    /// <summary>
    /// 保护排空状态和在途任务计数的同步锁。
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// 在途任务全部退出时完成的通知任务。
    /// </summary>
    private readonly TaskCompletionSource<bool> _drained = CreatePendingSource();

    /// <summary>
    /// 指示运行时是否正在排空。
    /// </summary>
    private bool _draining;

    /// <summary>
    /// 当前正在执行的任务数。
    /// </summary>
    private int _inFlight;

    /// <inheritdoc />
    public bool IsDraining
    {
        get
        {
            lock (_gate)
                return _draining;
        }
    }

    /// <inheritdoc />
    public int InFlightCount => Volatile.Read(ref _inFlight);

    /// <inheritdoc />
    public void BeginDrain()
    {
        lock (_gate)
        {
            if (_draining)
                return;

            _draining = true;
            if (_inFlight == 0)
                _drained.TrySetResult(true);
        }
    }

    /// <inheritdoc />
    public async Task<bool> WaitForDrainAsync(TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout < Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        Task drained;
        lock (_gate)
        {
            if (!_draining)
                return false;
            drained = _drained.Task;
        }

        if (drained.IsCompleted)
            return true;

        try
        {
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                await drained.WaitAsync(cancellationToken);
                return true;
            }

            await drained.WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试登记一个新进入的任务。
    /// </summary>
    /// <returns>尚未开始排空且登记成功时返回 <see langword="true"/>。</returns>
    internal bool TryEnter()
    {
        lock (_gate)
        {
            if (_draining)
                return false;
            _inFlight++;
            return true;
        }
    }

    /// <summary>
    /// 登记一个任务已退出。
    /// </summary>
    internal void Exit()
    {
        lock (_gate)
        {
            if (_inFlight <= 0)
                throw new InvalidOperationException("Hangfire 在途任务计数不平衡。");

            _inFlight--;
            if (_draining && _inFlight == 0)
                _drained.TrySetResult(true);
        }
    }

    /// <summary>
    /// 创建尚未完成的排空通知源。
    /// </summary>
    /// <returns>异步延续不会同步执行的通知源。</returns>
    private static TaskCompletionSource<bool> CreatePendingSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
