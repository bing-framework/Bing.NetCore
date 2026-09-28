namespace Bing.Core.Modularity;

/// <summary>
/// 模块资源释放辅助方法。
/// </summary>
internal static class BingModuleDisposal
{
    /// <summary>
    /// 在默认线程池上同步等待异步释放，避免调用方同步上下文被异步清理回调阻塞。
    /// </summary>
    /// <param name="disposable">需要同步等待释放完成的异步资源。</param>
    internal static void DisposeSynchronously(IAsyncDisposable disposable)
    {
        Task.Run(async () => await disposable.DisposeAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
}
