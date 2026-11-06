namespace Bing.Auditing;

/// <summary>
/// 提供审计管理器的异步作用域入口。
/// </summary>
public static class AuditingManagerExtensions
{
    /// <summary>
    /// 开始支持异步释放的审计作用域。
    /// </summary>
    /// <param name="manager">提供审计作用域的管理器。</param>
    /// <returns>支持异步释放的审计作用域保存句柄。</returns>
    public static IAsyncAuditLogSaveHandle BeginAsyncScope(this IAuditingManager manager)
    {
        if (manager == null)
            throw new ArgumentNullException(nameof(manager));
        var handle = manager.BeginScope();
        return handle as IAsyncAuditLogSaveHandle ?? new AsyncAuditLogSaveHandleAdapter(handle);
    }

    /// <summary>
    /// 为旧审计管理器适配异步释放。
    /// </summary>
    private sealed class AsyncAuditLogSaveHandleAdapter : IAsyncAuditLogSaveHandle, ICancellableAuditLogSaveHandle
    {
        /// <summary>
        /// 旧作用域。
        /// </summary>
        private readonly IAuditLogSaveHandle _handle;

        /// <summary>
        /// 同步保存与释放状态。
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>
        /// 已开始的保存任务。
        /// </summary>
        private Task _saveTask;

        /// <summary>
        /// 释放任务。
        /// </summary>
        private Task _disposeTask;

        /// <summary>
        /// 初始化一个 <see cref="AsyncAuditLogSaveHandleAdapter" /> 类型的实例。
        /// </summary>
        /// <param name="handle">用于标识或访问当前任务、操作或资源的句柄。</param>
        internal AsyncAuditLogSaveHandleAdapter(IAuditLogSaveHandle handle) =>
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));

        /// <inheritdoc />
        public Task SaveAsync()
        {
            return SaveCore(CancellationToken.None);
        }

        /// <inheritdoc />
        public Task SaveAsync(CancellationToken cancellationToken)
        {
            return SaveCore(cancellationToken);
        }

        /// <summary>
        /// 开始或复用适配句柄的审计保存任务。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <remarks>首次保存使用指定取消令牌；后续调用复用已创建的保存任务。</remarks>
        private Task SaveCore(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_disposeTask != null)
                    throw new ObjectDisposedException(nameof(AsyncAuditLogSaveHandleAdapter));
                if (_saveTask != null)
                    return _saveTask;
                try
                {
                    var task = _handle is ICancellableAuditLogSaveHandle cancellable
                        ? cancellable.SaveAsync(cancellationToken)
                        : _handle.SaveAsync();
                    return _saveTask = task ??
                        Task.FromException(new InvalidOperationException("审计存储返回了空保存任务。"));
                }
                catch (Exception exception)
                {
                    return _saveTask = Task.FromException(exception);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposeTask != null)
                    return;
                if (_saveTask != null && !_saveTask.IsCompleted)
                    throw new InvalidOperationException("审计保存尚未完成，请使用异步释放。");
                _disposeTask = Task.CompletedTask;
                _handle.Dispose();
            }
            _saveTask?.GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            lock (_gate)
            {
                if (_disposeTask == null)
                    _disposeTask = DisposeCoreAsync(_handle, _saveTask);
                return new ValueTask(_disposeTask);
            }
        }

        /// <summary>
        /// 等待保存后释放旧作用域。
        /// </summary>
        /// <param name="handle">需要等待保存并释放的审计句柄。</param>
        /// <param name="saveTask">已启动的审计保存任务；为 null 时直接释放句柄。</param>
        private static async Task DisposeCoreAsync(IAuditLogSaveHandle handle, Task saveTask)
        {
            try
            {
                if (saveTask != null)
                    await saveTask.ConfigureAwait(false);
            }
            finally
            {
                if (handle is IAsyncDisposable asyncHandle)
                    await asyncHandle.DisposeAsync().ConfigureAwait(false);
                else
                    handle.Dispose();
            }
        }
    }
}
