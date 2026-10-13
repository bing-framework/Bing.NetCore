namespace Bing.EasyCaching;

/// <summary>
/// 协调 Hybrid 分布式读取与本地层回填的代次。
/// </summary>
internal sealed class HybridRefillGeneration
{
    /// <summary>
    /// 串行化代次递增与本地缓存更新。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>
    /// 当前本地回填代次；失效通知到达时递增。
    /// </summary>
    private long _value;

    /// <summary>
    /// 获取当前本地回填代次。
    /// </summary>
    /// <returns>当前代次值。</returns>
    internal long Capture() => Interlocked.Read(ref _value);

    /// <summary>
    /// 递增本地回填代次，使先前读取结果失效。
    /// </summary>
    internal void Advance()
    {
        _gate.Wait();
        try { Interlocked.Increment(ref _value); }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// 在代次未变化时同步应用本地缓存更新。
    /// </summary>
    /// <param name="expected">读取开始时捕获的代次。</param>
    /// <param name="action">本地缓存更新操作。</param>
    /// <returns>代次匹配并执行更新时返回 <see langword="true"/>。</returns>
    internal bool TryApply(long expected, Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        _gate.Wait();
        try
        {
            if (Interlocked.Read(ref _value) != expected)
                return false;
            action();
            return true;
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// 在代次未变化时异步应用本地缓存更新。
    /// </summary>
    /// <param name="expected">读取开始时捕获的代次。</param>
    /// <param name="action">本地缓存更新操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>代次匹配并执行更新时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    internal async Task<bool> TryApplyAsync(long expected, Func<Task> action,
        CancellationToken cancellationToken)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Interlocked.Read(ref _value) != expected)
                return false;
            await action().ConfigureAwait(false);
            return true;
        }
        finally { _gate.Release(); }
    }
}
