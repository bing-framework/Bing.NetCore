namespace Bing.Caching;

/// <summary>
/// 管理缓存回源委托的等待、取消与清理。
/// </summary>
/// <remarks>
/// 取消等待不会强制中止用户委托；清理失败时保留已有主异常并附加清理错误。
/// </remarks>
internal static class CacheFactoryExecution
{
    /// <summary>
    /// 在指定生命周期和调用方取消范围内执行回源委托。
    /// </summary>
    /// <typeparam name="T">回源结果类型。</typeparam>
    /// <param name="factory">接收生命周期令牌并返回回源任务的委托。</param>
    /// <param name="lifetime">表示协调租约或回源时限的生命周期令牌。</param>
    /// <param name="caller">表示调用方取消请求的令牌。</param>
    /// <returns>异步返回回源委托的结果。</returns>
    internal static async Task<T> InvokeAsync<T>(Func<CancellationToken, Task<T>> factory,
        CancellationToken lifetime, CancellationToken caller)
    {
        ThrowIfCancelled(lifetime, caller);
        var task = factory(lifetime) ?? throw new InvalidOperationException("缓存回源委托不能返回 null Task。");
        return await AwaitAsync(task, lifetime, caller).ConfigureAwait(false);
    }

    /// <summary>
    /// 等待回源任务完成或生命周期取消。
    /// </summary>
    /// <typeparam name="T">回源结果类型。</typeparam>
    /// <param name="task">正在执行的回源任务。</param>
    /// <param name="lifetime">表示协调租约或回源时限的生命周期令牌。</param>
    /// <param name="caller">表示调用方取消请求的令牌。</param>
    /// <returns>异步返回回源任务的结果。</returns>
    internal static async Task<T> AwaitAsync<T>(Task<T> task, CancellationToken lifetime, CancellationToken caller)
    {
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (lifetime.Register(() => signal.TrySetResult(true)))
        {
            if (await Task.WhenAny(task, signal.Task).ConfigureAwait(false) != task)
            {
                Observe(task);
                ThrowIfCancelled(lifetime, caller);
            }
            try
            {
                var value = await task.ConfigureAwait(false);
                ThrowIfCancelled(lifetime, caller);
                return value;
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                ThrowIfCancelled(lifetime, caller);
                throw;
            }
        }
    }

    /// <summary>
    /// 根据调用方和回源生命周期令牌抛出取消或超时异常。
    /// </summary>
    /// <param name="lifetime">表示协调租约或回源时限的生命周期令牌。</param>
    /// <param name="caller">表示调用方取消请求的令牌。</param>
    internal static void ThrowIfCancelled(CancellationToken lifetime, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (lifetime.IsCancellationRequested)
            throw new TimeoutException("缓存回源超时或协调已失效，结果未写回。");
    }

    /// <summary>
    /// 观察已停止等待的任务，避免其后续异常未被读取。
    /// </summary>
    /// <param name="task">需要观察的异步任务。</param>
    internal static void Observe(Task task) => task.ContinueWith(completed => { _ = completed.Exception; },
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    /// <summary>
    /// 执行同步清理并保留先前发生的主异常。
    /// </summary>
    /// <param name="cleanup">需要执行的清理操作。</param>
    /// <param name="primary">此前发生的主异常；为 <see langword="null"/> 时清理异常直接传播。</param>
    internal static void Cleanup(Action cleanup, Exception primary)
    {
        try { cleanup(); }
        catch (Exception error)
        {
            if (primary == null) throw;
            primary.Data["CacheCleanupException"] = error;
        }
    }

    /// <summary>
    /// 在限定时长内异步执行清理并保留先前发生的主异常。
    /// </summary>
    /// <param name="cleanup">需要执行的异步清理操作。</param>
    /// <param name="primary">此前发生的主异常；为 <see langword="null"/> 时清理异常直接传播。</param>
    /// <param name="limit">清理操作的最长等待时长。</param>
    internal static async Task CleanupAsync(Func<Task> cleanup, Exception primary, TimeSpan limit)
    {
        try
        {
            using var timeout = new CancellationTokenSource(limit);
            var task = cleanup();
            await AwaitAsync(CompleteAsync(task), timeout.Token, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            if (primary == null) throw;
            primary.Data["CacheCleanupException"] = error;
        }
    }

    /// <summary>
    /// 将不返回值的任务转换为成功标志任务。
    /// </summary>
    /// <param name="task">需要等待的任务。</param>
    /// <returns>任务成功完成后返回 <see langword="true"/>。</returns>
    private static async Task<bool> CompleteAsync(Task task)
    {
        await task.ConfigureAwait(false);
        return true;
    }
}
