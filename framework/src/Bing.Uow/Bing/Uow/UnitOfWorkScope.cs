using System.Runtime.ExceptionServices;
using System.Threading;

namespace Bing.Uow;

/// <summary>
/// 管理一个 root 工作单元中的资源状态和提交结果。
/// </summary>
internal sealed class UnitOfWorkScope : IUnitOfWorkExecutionScope
{
    private readonly object _sync = new();
    private readonly UnitOfWorkScopeManager _manager;
    private readonly UnitOfWorkScopeOptions _options;
    private readonly IUnitOfWorkSaveChangesHandler[] _saveChangesHandlers;
    private readonly bool _operationSavingEnabled;
    private readonly CancellationTokenSource _deadlineSource;
    private readonly CancellationTokenSource _linkedCancellationSource;
    private readonly AsyncLocal<bool> _savingExecutionFlow = new();
    private readonly Dictionary<object, UnitOfWorkResourceEntry> _resources = new();
    private readonly List<UnitOfWorkResourceEntry> _resourceOrder = new();
    private readonly List<Func<Task>> _completedCallbacks = new();
    private UnitOfWorkScopeFrame _frame;
    private UnitOfWorkScopeState _state = UnitOfWorkScopeState.Active;
    private bool _rollbackOnly;
    private bool _disposing;
    private int _participants;
    private int _pendingResourceFactories;

    public UnitOfWorkScope(UnitOfWorkScopeManager manager, UnitOfWorkScopeOptions options,
        IUnitOfWorkSaveChangesHandler[] saveChangesHandlers, bool operationSavingEnabled,
        CancellationToken cancellationToken, CancellationTokenSource deadlineSource,
        CancellationTokenSource linkedCancellationSource)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _options = Clone(options ?? throw new ArgumentNullException(nameof(options)));
        _saveChangesHandlers = saveChangesHandlers ?? Array.Empty<IUnitOfWorkSaveChangesHandler>();
        _operationSavingEnabled = operationSavingEnabled;
        CancellationToken = cancellationToken;
        _deadlineSource = deadlineSource;
        _linkedCancellationSource = linkedCancellationSource;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public UnitOfWorkScopeOptions Options => Clone(_options);

    public CancellationToken CancellationToken { get; }

    public bool IsCompleted
    {
        get { lock (_sync) return _state == UnitOfWorkScopeState.Completed; }
    }

    public bool IsActive
    {
        get
        {
            lock (_sync)
                return (_state == UnitOfWorkScopeState.Active ||
                        _state == UnitOfWorkScopeState.Saving && _savingExecutionFlow.Value) && !_disposing;
        }
    }

    public bool IsRollbackOnly
    {
        get { lock (_sync) return _rollbackOnly; }
    }

    public bool IsDisposed
    {
        get { lock (_sync) return _state == UnitOfWorkScopeState.Disposed || _disposing; }
    }

    public void AttachFrame(UnitOfWorkScopeFrame frame) => _frame = frame;

    public void EnsureCanParticipate(UnitOfWorkScopeOptions options)
    {
        lock (_sync)
        {
            if (_state != UnitOfWorkScopeState.Active || _disposing)
            {
                if (_state == UnitOfWorkScopeState.Saving)
                    _rollbackOnly = true;
                throw new InvalidOperationException("root 工作单元已进入完成阶段，不能再创建参与范围。");
            }
            if (options == null)
                return;
            var snapshot = ValidateAndClone(options);
            if (snapshot.IsTransactional != _options.IsTransactional ||
                snapshot.IsolationLevel != _options.IsolationLevel || snapshot.Timeout != _options.Timeout)
                throw new InvalidOperationException("参与式嵌套的事务选项必须与 root 工作单元一致。");
        }
    }

    public UnitOfWorkScopeParticipant CreateParticipant(CancellationToken parentCancellationToken,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_state != UnitOfWorkScopeState.Active || _disposing)
                throw new InvalidOperationException("root 工作单元已进入完成阶段，不能再创建参与范围。");
            EnsureActive();
            _participants++;
            var linkedToken = UnitOfWorkScopeManager.CreateParticipantCancellation(parentCancellationToken,
                cancellationToken);
            return new UnitOfWorkScopeParticipant(this, linkedToken.Token, linkedToken.Source);
        }
    }

    public void EnsureParticipantCanExit(UnitOfWorkScopeFrame frame)
    {
        _manager.ValidateExit(frame);
        lock (_sync)
        {
            if (_state != UnitOfWorkScopeState.Active || _disposing)
                throw new InvalidOperationException("root 工作单元已进入终态，参与范围不能继续退出。");
        }
    }

    public void ExitParticipant(UnitOfWorkScopeFrame frame) => _manager.Exit(frame);

    internal void CloseParticipant(bool completed)
    {
        lock (_sync)
        {
            if (_participants <= 0)
                throw new InvalidOperationException("工作单元参与范围计数已失效。");
            if (!completed)
                _rollbackOnly = true;
            _participants--;
        }
    }

    public TResource GetOrAddResource<TResource>(object key, Func<TResource> factory)
        where TResource : class, IUnitOfWorkResource
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        UnitOfWorkResourceEntry entry;
        lock (_sync)
        {
            EnsureActive();
            if (_resources.TryGetValue(key, out entry))
            {
                if (entry.ResourceType != typeof(TResource))
                    throw new InvalidOperationException("相同工作单元资源键不能对应不同资源类型。");
            }
            else
            {
                entry = new UnitOfWorkResourceEntry(typeof(TResource), () => factory()) { UnitOfWorkId = Id };
                _resources.Add(key, entry);
                _resourceOrder.Add(entry);
                _pendingResourceFactories++;
            }
        }

        try
        {
            var resource = entry.GetResource();
            if (resource is not TResource typedResource)
                throw new InvalidOperationException("工作单元资源工厂返回了不匹配的资源类型。");
            return typedResource;
        }
        catch
        {
            lock (_sync)
            {
                if (entry.TryFinishFactory())
                    _pendingResourceFactories--;
                if (_resources.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                {
                    _resources.Remove(key);
                    _resourceOrder.Remove(entry);
                }
            }
            throw;
        }
        finally
        {
            lock (_sync)
            {
                if (entry.TryFinishFactory())
                    _pendingResourceFactories--;
            }
        }
    }

    public IDisposable AcquireExecutionLease(object resourceKey)
    {
        if (resourceKey == null)
            throw new ArgumentNullException(nameof(resourceKey));
        lock (_sync)
        {
            EnsureActive();
            if (!_resources.TryGetValue(resourceKey, out var entry) || !entry.IsResourceCreated)
                throw new InvalidOperationException("只能为当前工作单元中已创建的资源获取执行租约。");
            if (!entry.TryAcquireLease())
                throw new InvalidOperationException("同一工作单元资源已有活动执行，不能并发使用该连接。");
            return new UnitOfWorkResourceLease(entry);
        }
    }

    public void OnResourceCommitted(object resourceKey, Func<Task> callback)
    {
        if (resourceKey == null)
            throw new ArgumentNullException(nameof(resourceKey));
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        lock (_sync)
        {
            EnsureActive();
            if (!_resources.TryGetValue(resourceKey, out var entry) || !entry.IsResourceCreated)
                throw new InvalidOperationException("提交回调只能登记到当前工作单元中已创建的资源。");
            entry.AddCommittedCallback(callback);
        }
    }

    public void OnCompleted(Func<Task> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        lock (_sync)
        {
            EnsureActive();
            _completedCallbacks.Add(callback);
        }
    }

    public void SetRollbackOnly()
    {
        lock (_sync)
        {
            EnsureActive();
            _rollbackOnly = true;
        }
    }

    public void Complete() => CompleteCoreAsync(System.Threading.CancellationToken.None, synchronous: true)
        .GetAwaiter().GetResult();

    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        CompleteCoreAsync(cancellationToken, synchronous: false);

    private async Task CompleteCoreAsync(CancellationToken cancellationToken, bool synchronous)
    {
        var completionCancellation = CreateCompletionCancellation(cancellationToken);
        try
        {
            List<UnitOfWorkResourceEntry> resources;
            bool rollbackOnly;
            lock (_sync)
            {
                if (_state == UnitOfWorkScopeState.Completed)
                    return;
                if (_state != UnitOfWorkScopeState.Active || _disposing)
                {
                    if (_state == UnitOfWorkScopeState.Saving)
                        _rollbackOnly = true;
                    throw new InvalidOperationException("工作单元已进入完成阶段，不能重入完成。");
                }
                EnsureNoActiveHandles();
                rollbackOnly = _rollbackOnly;
                resources = _resourceOrder.ToList();
                _state = rollbackOnly
                    ? UnitOfWorkScopeState.RollingBack
                    : _operationSavingEnabled ? UnitOfWorkScopeState.Saving : UnitOfWorkScopeState.Completing;
            }

            if (rollbackOnly)
            {
                var rollbackErrors = new List<Exception>();
                if (synchronous)
                    RollbackResources(resources, rollbackErrors);
                else
                    await RollbackResourcesAsync(resources, rollbackErrors).ConfigureAwait(false);
                lock (_sync)
                    _state = UnitOfWorkScopeState.RolledBack;
                ThrowWithCleanup(new InvalidOperationException("工作单元已标记为仅回滚，不能完成提交。"),
                    rollbackErrors);
                return;
            }

            if (completionCancellation.Token.IsCancellationRequested)
            {
                await FailCompletionAsync(new OperationCanceledException(completionCancellation.Token), synchronous)
                    .ConfigureAwait(false);
                return;
            }

            if (_operationSavingEnabled)
            {
                _savingExecutionFlow.Value = true;
                try
                {
                    foreach (var handler in _saveChangesHandlers)
                    {
                        completionCancellation.Token.ThrowIfCancellationRequested();
                        if (synchronous)
                            handler.SaveChanges(this);
                        else
                            await handler.SaveChangesAsync(this, completionCancellation.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    _savingExecutionFlow.Value = false;
                    await FailCompletionAsync(exception, synchronous).ConfigureAwait(false);
                    return;
                }
                finally
                {
                    _savingExecutionFlow.Value = false;
                }

                Exception saveCompletionFailure = null;
                lock (_sync)
                {
                    if (_state != UnitOfWorkScopeState.Saving)
                    {
                        saveCompletionFailure = new InvalidOperationException(
                            "保存处理器改变了工作单元状态，不能继续提交。");
                    }
                    else if (_rollbackOnly)
                    {
                        saveCompletionFailure = new InvalidOperationException(
                            "保存处理器已将工作单元标记为仅回滚，不能完成提交。");
                    }
                    else
                    {
                        try
                        {
                            EnsureNoActiveHandles();
                            resources = _resourceOrder.ToList();
                            _state = UnitOfWorkScopeState.Completing;
                        }
                        catch (Exception exception)
                        {
                            saveCompletionFailure = exception;
                        }
                    }
                }

                if (saveCompletionFailure != null)
                {
                    await FailCompletionAsync(saveCompletionFailure, synchronous).ConfigureAwait(false);
                    return;
                }
            }

            if (completionCancellation.Token.IsCancellationRequested)
            {
                await FailCompletionAsync(new OperationCanceledException(completionCancellation.Token), synchronous)
                    .ConfigureAwait(false);
                return;
            }

            var notificationErrors = new List<Exception>();
            for (var index = 0; index < resources.Count; index++)
            {
                var entry = resources[index];
                try
                {
                    if (synchronous)
                        entry.Resource.Commit();
                    else
                    {
                        completionCancellation.Token.ThrowIfCancellationRequested();
                        await entry.Resource.CommitAsync(completionCancellation.Token).ConfigureAwait(false);
                    }
                    entry.MarkCommitted();
                }
                catch (Exception exception)
                {
                    entry.MarkCommitUnknown();
                    AttachResourceContext(exception, entry, index, "FailedOrUnknown");
                    var cleanupErrors = new List<Exception>(notificationErrors);
                    if (synchronous)
                        RollbackUncommitted(resources, index, cleanupErrors);
                    else
                        await RollbackUncommittedAsync(resources, index, cleanupErrors).ConfigureAwait(false);
                    lock (_sync)
                        _state = UnitOfWorkScopeState.Faulted;
                    if (_operationSavingEnabled)
                        ThrowWithCleanup(exception, cleanupErrors);
                    else
                    {
                        var errors = new List<Exception> { exception };
                        errors.AddRange(cleanupErrors);
                        ThrowCollected(errors);
                    }
                    return;
                }

                foreach (var callback in entry.CommittedCallbacks)
                {
                    try
                    {
                        var task = callback();
                        if (task == null)
                            throw new InvalidOperationException("工作单元提交回调不能返回 null Task。");
                        if (synchronous)
                            task.GetAwaiter().GetResult();
                        else
                            await task.ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        AttachResourceContext(exception, entry, index, "CommittedCallbackFailed");
                        notificationErrors.Add(exception);
                    }
                }
            }

            lock (_sync)
                _state = UnitOfWorkScopeState.Completed;

            foreach (var callback in _completedCallbacks)
            {
                try
                {
                    var task = callback();
                    if (task == null)
                        throw new InvalidOperationException("工作单元完成回调不能返回 null Task。");
                    if (synchronous)
                        task.GetAwaiter().GetResult();
                    else
                        await task.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    notificationErrors.Add(exception);
                }
            }
            ThrowCollected(notificationErrors);
        }
        finally
        {
            completionCancellation.Source?.Dispose();
        }
    }

    private async Task FailCompletionAsync(Exception primaryException, bool synchronous)
    {
        List<UnitOfWorkResourceEntry> resources;
        lock (_sync)
        {
            _rollbackOnly = true;
            _state = UnitOfWorkScopeState.RollingBack;
            resources = _resourceOrder.ToList();
        }

        var cleanupErrors = new List<Exception>();
        if (synchronous)
            RollbackResources(resources, cleanupErrors);
        else
            await RollbackResourcesAsync(resources, cleanupErrors).ConfigureAwait(false);
        lock (_sync)
            _state = primaryException is OperationCanceledException
                ? UnitOfWorkScopeState.RolledBack
                : UnitOfWorkScopeState.Faulted;
        ThrowWithCleanup(primaryException, cleanupErrors);
    }

    private CompletionCancellation CreateCompletionCancellation(CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || cancellationToken == CancellationToken)
            return new CompletionCancellation(CancellationToken, null);
        if (!CancellationToken.CanBeCanceled)
            return new CompletionCancellation(cancellationToken, null);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cancellationToken);
        return new CompletionCancellation(linked.Token, linked);
    }

    public void Rollback() => RollbackCore(synchronous: true).GetAwaiter().GetResult();

    public Task RollbackAsync(CancellationToken cancellationToken = default) => RollbackCore(synchronous: false);

    private async Task RollbackCore(bool synchronous)
    {
        List<UnitOfWorkResourceEntry> resources;
        lock (_sync)
        {
            if (_state is UnitOfWorkScopeState.RolledBack or UnitOfWorkScopeState.Completed or UnitOfWorkScopeState.Disposed)
                return;
            if (_state == UnitOfWorkScopeState.Saving)
            {
                _rollbackOnly = true;
                throw new InvalidOperationException("保存处理器不能重入回滚工作单元。");
            }
            EnsureActive();
            EnsureNoActiveHandles();
            resources = _resourceOrder.ToList();
            _state = UnitOfWorkScopeState.RollingBack;
        }

        var errors = new List<Exception>();
        if (synchronous)
            RollbackResources(resources, errors);
        else
            await RollbackResourcesAsync(resources, errors).ConfigureAwait(false);
        lock (_sync)
            _state = UnitOfWorkScopeState.RolledBack;
        ThrowCollected(errors);
    }

    public void Dispose()
    {
        var cleanup = BeginDispose();
        var errors = new List<Exception>();
        if (cleanup.RollbackResources)
            RollbackResources(cleanup.Resources, errors);
        DisposeResources(cleanup.Resources, errors);
        FinishDispose();
        ThrowCollected(errors);
    }

    public ValueTask DisposeAsync()
    {
        var cleanup = BeginDispose();
        return new ValueTask(DisposeAsyncCore(cleanup));
    }

    private async Task DisposeAsyncCore(DisposeSnapshot cleanup)
    {
        var errors = new List<Exception>();
        if (cleanup.RollbackResources)
            await RollbackResourcesAsync(cleanup.Resources, errors).ConfigureAwait(false);
        await DisposeResourcesAsync(cleanup.Resources, errors).ConfigureAwait(false);
        FinishDispose();
        ThrowCollected(errors);
    }

    private DisposeSnapshot BeginDispose()
    {
        lock (_sync)
        {
            if (_state == UnitOfWorkScopeState.Disposed || _disposing)
                return new DisposeSnapshot(new List<UnitOfWorkResourceEntry>(), rollbackResources: false, alreadyDisposed: true);
        }
        _manager.ValidateExit(_frame);
        List<UnitOfWorkResourceEntry> resources;
        bool rollbackResources;
        lock (_sync)
        {
            if (_state == UnitOfWorkScopeState.Disposed || _disposing)
                return new DisposeSnapshot(new List<UnitOfWorkResourceEntry>(), rollbackResources: false, alreadyDisposed: true);
            if (_state == UnitOfWorkScopeState.Saving)
            {
                _rollbackOnly = true;
                throw new InvalidOperationException("保存处理器不能重入释放工作单元。");
            }
            if (_state is UnitOfWorkScopeState.Completing or UnitOfWorkScopeState.RollingBack)
                throw new InvalidOperationException("工作单元正在完成或回滚，不能并发释放。");
            EnsureNoActiveHandles();
            rollbackResources = _state is UnitOfWorkScopeState.Active or UnitOfWorkScopeState.Faulted;
            resources = _resourceOrder.ToList();
            _disposing = true;
        }

        _manager.Exit(_frame);
        return new DisposeSnapshot(resources, rollbackResources, alreadyDisposed: false);
    }

    private void FinishDispose()
    {
        lock (_sync)
        {
            _state = UnitOfWorkScopeState.Disposed;
            _disposing = false;
        }
        _linkedCancellationSource?.Dispose();
        _deadlineSource?.Dispose();
    }

    private void EnsureNoActiveHandles()
    {
        if (_participants != 0)
            throw new InvalidOperationException("仍有未退出的参与式工作单元范围。");
        if (_pendingResourceFactories != 0)
            throw new InvalidOperationException("仍有工作单元资源正在登记。");
        if (_resourceOrder.Any(resource => resource.HasActiveLease))
            throw new InvalidOperationException("仍有 SQL 执行或 reader 持有工作单元资源租约。");
    }

    private void EnsureActive()
    {
        if (_state == UnitOfWorkScopeState.Saving && !_savingExecutionFlow.Value)
        {
            _rollbackOnly = true;
            throw new InvalidOperationException("保存期间只允许保存处理器执行流访问工作单元。");
        }
        if ((_state != UnitOfWorkScopeState.Active &&
             !(_state == UnitOfWorkScopeState.Saving && _savingExecutionFlow.Value)) || _disposing)
            throw new InvalidOperationException("工作单元已进入终态，不能继续使用或登记资源。");
    }

    private static UnitOfWorkScopeOptions ValidateAndClone(UnitOfWorkScopeOptions options) =>
        UnitOfWorkScopeManager.CloneAndValidateOptions(options);

    private static UnitOfWorkScopeOptions Clone(UnitOfWorkScopeOptions options) => new()
    {
        IsTransactional = options.IsTransactional,
        IsolationLevel = options.IsolationLevel,
        Timeout = options.Timeout
    };

    private void RollbackUncommitted(IReadOnlyList<UnitOfWorkResourceEntry> resources, int failedIndex,
        ICollection<Exception> errors)
    {
        for (var index = resources.Count - 1; index >= failedIndex; index--)
        {
            var entry = resources[index];
            if (entry.IsCommitted || !entry.TryBeginRollback())
                continue;
            try
            {
                entry.Resource.Rollback();
                entry.MarkRolledBack();
            }
            catch (Exception exception)
            {
                AttachResourceContext(exception, entry, index, "RollbackFailed");
                errors.Add(exception);
            }
        }
    }

    private async Task RollbackUncommittedAsync(IReadOnlyList<UnitOfWorkResourceEntry> resources, int failedIndex,
        ICollection<Exception> errors)
    {
        for (var index = resources.Count - 1; index >= failedIndex; index--)
        {
            var entry = resources[index];
            if (entry.IsCommitted || !entry.TryBeginRollback())
                continue;
            try
            {
                await entry.Resource.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                entry.MarkRolledBack();
            }
            catch (Exception exception)
            {
                AttachResourceContext(exception, entry, index, "RollbackFailed");
                errors.Add(exception);
            }
        }
    }

    private void RollbackResources(IReadOnlyList<UnitOfWorkResourceEntry> resources, ICollection<Exception> errors)
    {
        for (var index = resources.Count - 1; index >= 0; index--)
        {
            var entry = resources[index];
            if (entry.IsCommitted || !entry.TryBeginRollback())
                continue;
            try
            {
                entry.Resource.Rollback();
                entry.MarkRolledBack();
            }
            catch (Exception exception)
            {
                AttachResourceContext(exception, entry, index, "RollbackFailed");
                errors.Add(exception);
            }
        }
    }

    private async Task RollbackResourcesAsync(IReadOnlyList<UnitOfWorkResourceEntry> resources,
        ICollection<Exception> errors)
    {
        for (var index = resources.Count - 1; index >= 0; index--)
        {
            var entry = resources[index];
            if (entry.IsCommitted || !entry.TryBeginRollback())
                continue;
            try
            {
                await entry.Resource.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                entry.MarkRolledBack();
            }
            catch (Exception exception)
            {
                AttachResourceContext(exception, entry, index, "RollbackFailed");
                errors.Add(exception);
            }
        }
    }

    private static void DisposeResources(IReadOnlyList<UnitOfWorkResourceEntry> resources, ICollection<Exception> errors)
    {
        for (var index = resources.Count - 1; index >= 0; index--)
        {
            var entry = resources[index];
            if (!entry.TryBeginDispose())
                continue;
            try
            {
                entry.Resource.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
    }

    private static async Task DisposeResourcesAsync(IReadOnlyList<UnitOfWorkResourceEntry> resources,
        ICollection<Exception> errors)
    {
        for (var index = resources.Count - 1; index >= 0; index--)
        {
            var entry = resources[index];
            if (!entry.TryBeginDispose())
                continue;
            try
            {
                await entry.Resource.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
    }

    private static void AttachResourceContext(Exception exception, UnitOfWorkResourceEntry entry, int index, string outcome)
    {
        try
        {
            exception.Data["Bing.Uow.Id"] = entry.UnitOfWorkId;
            exception.Data["Bing.Uow.ResourceSequence"] = index;
            exception.Data["Bing.Uow.ResourceOutcome"] = outcome;
        }
        catch
        {
            // Exception.Data 仅提供诊断辅助信息，不能覆盖原始提交错误。
        }
    }

    private void ThrowWithCleanup(Exception primary, ICollection<Exception> cleanupErrors)
    {
        if (cleanupErrors.Count == 0)
            ExceptionDispatchInfo.Capture(primary).Throw();
        if (_operationSavingEnabled)
        {
            try
            {
                primary.Data["Bing.Operation.CleanupExceptions"] = cleanupErrors.ToArray();
            }
            catch
            {
                // 清理诊断不能替换操作异常。
            }
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        var errors = new List<Exception> { primary };
        errors.AddRange(cleanupErrors);
        ThrowCollected(errors);
    }

    private static void ThrowCollected(ICollection<Exception> errors)
    {
        if (errors.Count == 0)
            return;
        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors.First()).Throw();
        throw new AggregateException(errors);
    }

    private sealed class DisposeSnapshot
    {
        public DisposeSnapshot(List<UnitOfWorkResourceEntry> resources, bool rollbackResources, bool alreadyDisposed)
        {
            Resources = resources;
            RollbackResources = rollbackResources;
            AlreadyDisposed = alreadyDisposed;
        }

        public List<UnitOfWorkResourceEntry> Resources { get; }
        public bool RollbackResources { get; }
        public bool AlreadyDisposed { get; }
    }

    private readonly struct CompletionCancellation
    {
        public CompletionCancellation(CancellationToken token, CancellationTokenSource source)
        {
            Token = token;
            Source = source;
        }

        public CancellationToken Token { get; }
        public CancellationTokenSource Source { get; }
    }
}

/// <summary>
/// 参与 root 工作单元但不拥有资源提交权的嵌套范围。
/// </summary>
internal sealed class UnitOfWorkScopeParticipant : IUnitOfWorkExecutionScope
{
    private readonly UnitOfWorkScope _root;
    private readonly CancellationTokenSource _cancellationSource;
    private readonly CancellationToken _cancellationToken;
    private UnitOfWorkScopeFrame _frame;
    private bool _completed;
    private bool _disposed;

    public UnitOfWorkScopeParticipant(UnitOfWorkScope root, CancellationToken cancellationToken,
        CancellationTokenSource cancellationSource)
    {
        _root = root;
        _cancellationToken = cancellationToken;
        _cancellationSource = cancellationSource;
    }

    public Guid Id => _root.Id;
    public UnitOfWorkScopeOptions Options => new()
    {
        IsTransactional = _root.Options.IsTransactional,
        IsolationLevel = _root.Options.IsolationLevel,
        Timeout = _root.Options.Timeout
    };
    public CancellationToken CancellationToken => _cancellationToken;
    public bool IsCompleted => _root.IsCompleted;
    public bool IsActive => !_completed && !_disposed && _root.IsActive;
    public bool IsRollbackOnly => _root.IsRollbackOnly;
    public bool IsDisposed => _disposed;

    public void AttachFrame(UnitOfWorkScopeFrame frame) => _frame = frame;

    public TResource GetOrAddResource<TResource>(object key, Func<TResource> factory)
        where TResource : class, IUnitOfWorkResource => _root.GetOrAddResource(key, factory);

    public IDisposable AcquireExecutionLease(object resourceKey) => _root.AcquireExecutionLease(resourceKey);
    public void OnResourceCommitted(object resourceKey, Func<Task> callback) => _root.OnResourceCommitted(resourceKey, callback);
    public void OnCompleted(Func<Task> callback) => _root.OnCompleted(callback);
    public void SetRollbackOnly() => _root.SetRollbackOnly();

    public void Complete()
    {
        EnsureOpen();
        ThrowIfCancellationRequested();
        _completed = true;
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        ThrowIfCancellationRequested(cancellationToken);
        Complete();
        return Task.CompletedTask;
    }

    public void Rollback()
    {
        EnsureOpen();
        _root.SetRollbackOnly();
        _completed = true;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        Rollback();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _root.EnsureParticipantCanExit(_frame);
        _root.CloseParticipant(_completed);
        _root.ExitParticipant(_frame);
        _cancellationSource?.Dispose();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    private void EnsureOpen()
    {
        if (_disposed || _completed)
            throw new InvalidOperationException("参与式工作单元范围已完成或释放。");
    }

    private void ThrowIfCancellationRequested(CancellationToken additionalToken = default)
    {
        var token = _cancellationToken.IsCancellationRequested
            ? _cancellationToken
            : additionalToken;
        if (!token.IsCancellationRequested)
            return;
        _root.SetRollbackOnly();
        throw new OperationCanceledException(token);
    }
}

internal enum UnitOfWorkScopeState
{
    Active,
    Saving,
    Completing,
    Completed,
    RollingBack,
    RolledBack,
    Faulted,
    Disposed
}

/// <summary>
/// 保存一个 root 的资源、租约和逐资源成功通知。
/// </summary>
internal sealed class UnitOfWorkResourceEntry
{
    private readonly Lazy<IUnitOfWorkResource> _resource;
    private readonly List<Func<Task>> _committedCallbacks = new();
    private int _factoryFinished;
    private int _leaseActive;
    private int _disposeStarted;
    private int _rollbackAttempted;
    private int _commitState;

    public UnitOfWorkResourceEntry(Type resourceType, Func<IUnitOfWorkResource> factory)
    {
        ResourceType = resourceType;
        _resource = new Lazy<IUnitOfWorkResource>(() =>
            factory() ?? throw new InvalidOperationException("工作单元资源工厂返回了 null。"),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Type ResourceType { get; }
    public IUnitOfWorkResource Resource => GetResource();
    public Guid UnitOfWorkId { get; set; }
    public bool IsResourceCreated => _resource.IsValueCreated;
    public bool IsCommitted => Volatile.Read(ref _commitState) == 1;
    public bool IsCommitUnknown => Volatile.Read(ref _commitState) == 2;
    public bool IsRolledBack => Volatile.Read(ref _commitState) == 3;
    public bool HasActiveLease => Volatile.Read(ref _leaseActive) != 0;
    public IReadOnlyList<Func<Task>> CommittedCallbacks => _committedCallbacks;

    public IUnitOfWorkResource GetResource()
    {
        var resource = _resource.Value;
        return resource;
    }

    public bool TryFinishFactory() => Interlocked.Exchange(ref _factoryFinished, 1) == 0;
    public bool TryAcquireLease() => Interlocked.CompareExchange(ref _leaseActive, 1, 0) == 0;
    public void ReleaseLease() => Interlocked.Exchange(ref _leaseActive, 0);
    public void AddCommittedCallback(Func<Task> callback) => _committedCallbacks.Add(callback);
    public void MarkCommitted() => Volatile.Write(ref _commitState, 1);
    public void MarkCommitUnknown() => Volatile.Write(ref _commitState, 2);
    public bool TryBeginRollback() => Interlocked.CompareExchange(ref _rollbackAttempted, 1, 0) == 0;
    public void MarkRolledBack()
    {
        if (!IsCommitUnknown)
            Volatile.Write(ref _commitState, 3);
    }
    public bool TryBeginDispose() => Interlocked.Exchange(ref _disposeStarted, 1) == 0;
}

internal sealed class UnitOfWorkResourceLease : IDisposable
{
    private UnitOfWorkResourceEntry _entry;

    public UnitOfWorkResourceLease(UnitOfWorkResourceEntry entry) => _entry = entry;

    public void Dispose() => Interlocked.Exchange(ref _entry, null)?.ReleaseLease();
}
