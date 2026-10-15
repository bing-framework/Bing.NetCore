using System.Threading;

namespace Bing.Auditing;

/// <summary>
/// 管理当前异步执行流中的审计作用域。
/// </summary>
public sealed class AuditingManager : IAuditingManager
{
    /// <summary>
    /// 审计日志保存目标。
    /// </summary>
    private readonly IAuditingStore _store;

    /// <summary>
    /// 当前执行流的审计作用域。
    /// </summary>
    private readonly AsyncLocal<AuditHandle> _current = new AsyncLocal<AuditHandle>();

    /// <summary>
    /// 初始化一个 <see cref="AuditingManager" /> 类型的实例。
    /// </summary>
    /// <param name="store">审计日志保存目标。</param>
    public AuditingManager(IAuditingStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public IAuditLogScope Current => _current.Value?.Scope;

    /// <inheritdoc />
    public IAuditLogSaveHandle BeginScope()
    {
        var handle = new AuditHandle(this, _current.Value, _store);
        _current.Value = handle;
        return handle;
    }

    /// <summary>
    /// 保存作用域，并在释放后恢复上一层作用域。
    /// </summary>
    private sealed class AuditHandle : IAsyncAuditLogSaveHandle, ICancellableAuditLogSaveHandle
    {
        /// <summary>
        /// 所属管理器。
        /// </summary>
        private readonly AuditingManager _manager;

        /// <summary>
        /// 上一层作用域。
        /// </summary>
        private readonly AuditHandle _parent;

        /// <summary>
        /// 审计日志保存目标。
        /// </summary>
        private readonly IAuditingStore _store;

        /// <summary>
        /// 同步保存与释放状态。
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>
        /// 已发起的保存任务。
        /// </summary>
        private Task _saveTask;

        /// <summary>
        /// 异步释放任务。
        /// </summary>
        private Task _disposeTask;

        /// <summary>
        /// 释放状态。
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 初始化一个 <see cref="AuditHandle" /> 类型的实例。
        /// </summary>
        internal AuditHandle(AuditingManager manager, AuditHandle parent, IAuditingStore store)
        {
            _manager = manager;
            _parent = parent;
            _store = store;
            Scope = new AuditLogScope(new AuditLogInfo { ExecutionTime = DateTime.UtcNow });
        }

        /// <summary>
        /// 当前审计日志作用域。
        /// </summary>
        internal AuditLogScope Scope { get; }

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
        /// 使用指定取消令牌开始或复用当前保存任务。
        /// </summary>
        private Task SaveCore(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_disposed != 0)
                    throw new ObjectDisposedException(nameof(AuditHandle));
                if (_saveTask != null)
                    return _saveTask;
                try
                {
                    var task = _store is ICancellableAuditingStore cancellable
                        ? cancellable.SaveAsync(Scope.Log, cancellationToken)
                        : _store.SaveAsync(Scope.Log);
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
                if (_disposed != 0)
                    return;
                if (_saveTask != null && !_saveTask.IsCompleted)
                    throw new InvalidOperationException("审计保存尚未完成，请使用异步释放。");
                _disposed = 1;
                _disposeTask = _saveTask ?? Task.CompletedTask;
                RestoreParent();
            }
            _saveTask?.GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            lock (_gate)
            {
                if (_disposed == 0)
                {
                    _disposed = 1;
                    _disposeTask = _saveTask ?? Task.CompletedTask;
                    RestoreParent();
                }
                return new ValueTask(_disposeTask);
            }
        }

        /// <summary>
        /// 恢复外层审计作用域。
        /// </summary>
        private void RestoreParent()
        {
            if (ReferenceEquals(_manager._current.Value, this))
                _manager._current.Value = _parent;
        }
    }
}
