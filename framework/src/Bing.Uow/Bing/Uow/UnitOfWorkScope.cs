using System.Runtime.ExceptionServices;
using System.Threading;

namespace Bing.Uow;

/// <summary>
/// 管理一个 root 工作单元中的资源状态和提交结果。
/// </summary>
internal sealed class UnitOfWorkScope : IUnitOfWorkExecutionScope
{
    /// <summary>
    /// 同步工作单元作用域状态的并发访问。
    /// </summary>
    private readonly object _sync = new();
    /// <summary>
    /// 保存创建该作用域的工作单元作用域管理器。
    /// </summary>
    private readonly UnitOfWorkScopeManager _manager;
    /// <summary>
    /// 保存工作单元作用域配置。
    /// </summary>
    private readonly UnitOfWorkScopeOptions _options;
    /// <summary>
    /// 保存工作单元保存变更处理器数组。
    /// </summary>
    private readonly IUnitOfWorkSaveChangesHandler[] _saveChangesHandlers;
    /// <summary>
    /// 指示工作单元作用域是否启用操作级保存。
    /// </summary>
    private readonly bool _operationSavingEnabled;
    /// <summary>
    /// 保存工作单元作用域的截止时间取消源。
    /// </summary>
    private readonly CancellationTokenSource _deadlineSource;
    /// <summary>
    /// 保存与外部取消令牌链接的取消源。
    /// </summary>
    private readonly CancellationTokenSource _linkedCancellationSource;
    /// <summary>
    /// 保存异步本地的保存执行流标记。
    /// </summary>
    private readonly AsyncLocal<bool> _savingExecutionFlow = new();
    /// <summary>
    /// 保存按资源键索引的工作单元资源条目。
    /// </summary>
    private readonly Dictionary<object, UnitOfWorkResourceEntry> _resources = new();
    /// <summary>
    /// 保存按登记顺序排列的工作单元资源条目。
    /// </summary>
    private readonly List<UnitOfWorkResourceEntry> _resourceOrder = new();
    /// <summary>
    /// 保存工作单元完成时执行的回调集合。
    /// </summary>
    private readonly List<Func<Task>> _completedCallbacks = new();
    /// <summary>
    /// 保存工作单元作用域帧。
    /// </summary>
    private UnitOfWorkScopeFrame _frame;
    /// <summary>
    /// 保存工作单元作用域当前状态。
    /// </summary>
    private UnitOfWorkScopeState _state = UnitOfWorkScopeState.Active;
    /// <summary>
    /// 指示工作单元作用域是否只能回滚。
    /// </summary>
    private bool _rollbackOnly;
    /// <summary>
    /// 指示工作单元作用域是否正在释放。
    /// </summary>
    private bool _disposing;
    /// <summary>
    /// 记录工作单元作用域的参与者数量。
    /// </summary>
    private int _participants;
    /// <summary>
    /// 记录尚未完成的工作单元资源工厂数量。
    /// </summary>
    private int _pendingResourceFactories;

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkScope" /> 类型的实例。
    /// </summary>
    /// <param name="manager">记录当前测试工作单元操作的管理器。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="saveChangesHandlers">当前工作单元作用域使用的保存变更处理器集合。</param>
    /// <param name="operationSavingEnabled">指示当前工作单元是否启用操作记录保存。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <param name="deadlineSource">用于控制操作截止时间的令牌源。</param>
    /// <param name="linkedCancellationSource">与工作单元作用域关联的取消令牌源。</param>
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

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public UnitOfWorkScopeOptions Options => Clone(_options);

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc />
    public bool IsCompleted
    {
        get { lock (_sync) return _state == UnitOfWorkScopeState.Completed; }
    }

    /// <inheritdoc />
    public bool IsActive
    {
        get
        {
            lock (_sync)
                return (_state == UnitOfWorkScopeState.Active ||
                        _state == UnitOfWorkScopeState.Saving && _savingExecutionFlow.Value) && !_disposing;
        }
    }

    /// <inheritdoc />
    public bool IsRollbackOnly
    {
        get { lock (_sync) return _rollbackOnly; }
    }

    /// <inheritdoc />
    public bool IsDisposed
    {
        get { lock (_sync) return _state == UnitOfWorkScopeState.Disposed || _disposing; }
    }

    /// <summary>
    /// 关联由管理器创建的工作单元栈帧。
    /// </summary>
    /// <param name="frame">要附加到当前作用域的栈帧。</param>
    public void AttachFrame(UnitOfWorkScopeFrame frame) => _frame = frame;

    /// <summary>
    /// 验证给定选项的工作单元能否参与当前作用域。
    /// </summary>
    /// <param name="options">参与方的工作单元选项。</param>
    /// <remarks>
    /// 完成、释放或保存阶段不接受新参与者，并检查事务配置是否兼容。
    /// </remarks>
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

    /// <summary>
    /// 创建关联父取消状态的工作单元参与者。
    /// </summary>
    /// <param name="parentCancellationToken">父参与方的取消令牌。</param>
    /// <param name="cancellationToken">当前参与方自己的取消令牌。</param>
    /// <returns>使用父级和参与者取消令牌创建的工作单元参与范围。</returns>
    /// <remarks>
    /// 根作用域进入完成阶段或参与选项不兼容时抛出异常。
    /// </remarks>
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

    /// <summary>
    /// 验证指定参与者能否退出工作单元作用域。
    /// </summary>
    /// <param name="frame">表示待退出参与者的栈帧。</param>
    /// <remarks>
    /// 由作用域管理器验证退出顺序，并确保当前 root 仍允许退出。
    /// </remarks>
    public void EnsureParticipantCanExit(UnitOfWorkScopeFrame frame)
    {
        _manager.ValidateExit(frame);
        lock (_sync)
        {
            if (_state != UnitOfWorkScopeState.Active || _disposing)
                throw new InvalidOperationException("root 工作单元已进入终态，参与范围不能继续退出。");
        }
    }

    /// <summary>
    /// 退出指定工作单元参与者。
    /// </summary>
    /// <param name="frame">表示待退出参与者的栈帧。</param>
    public void ExitParticipant(UnitOfWorkScopeFrame frame) => _manager.Exit(frame);

    /// <summary>
    /// 关闭一个工作单元参与者并更新参与计数。
    /// </summary>
    /// <param name="completed">参与者是否正常完成。</param>
    /// <remarks>
    /// 参与计数失效时抛出异常；最后一个失败参与者会使 root 仅回滚。
    /// </remarks>
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void SetRollbackOnly()
    {
        lock (_sync)
        {
            EnsureActive();
            _rollbackOnly = true;
        }
    }

    /// <inheritdoc />
    public void Complete() => CompleteCoreAsync(System.Threading.CancellationToken.None, synchronous: true)
        .GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task CompleteAsync(CancellationToken cancellationToken = default) =>
        CompleteCoreAsync(cancellationToken, synchronous: false);

    /// <summary>
    /// 完成工作单元并按事务顺序提交资源及通知回调。
    /// </summary>
    /// <param name="cancellationToken">用于取消提交阶段的令牌。</param>
    /// <param name="synchronous">是否按同步完成路径调用资源操作。</param>
    /// <remarks>
    /// 提交失败时执行回滚清理；提交完成后运行已登记的完成通知。
    /// </remarks>
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

    /// <summary>
    /// 记录工作单元完成失败并回滚未完成资源。
    /// </summary>
    /// <param name="primaryException">触发完成失败的原始异常。</param>
    /// <param name="synchronous">是否按同步回滚路径调用资源操作。</param>
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

    /// <summary>
    /// 根据工作单元取消令牌创建完成阶段的取消上下文。
    /// </summary>
    /// <param name="cancellationToken">调用方提供的取消令牌。</param>
    /// <returns>包含完成操作有效取消令牌及其可选链接源的结果。</returns>
    /// <remarks>
    /// 必要时组合外部取消和工作单元内部取消来源。
    /// </remarks>
    private CompletionCancellation CreateCompletionCancellation(CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || cancellationToken == CancellationToken)
            return new CompletionCancellation(CancellationToken, null);
        if (!CancellationToken.CanBeCanceled)
            return new CompletionCancellation(cancellationToken, null);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cancellationToken);
        return new CompletionCancellation(linked.Token, linked);
    }

    /// <inheritdoc />
    public void Rollback() => RollbackCore(synchronous: true).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken = default) => RollbackCore(synchronous: false);

    /// <summary>
    /// 按逆序回滚尚未完成的工作单元资源。
    /// </summary>
    /// <param name="synchronous">是否按同步资源接口回滚。</param>
    /// <remarks>
    /// 已回滚、完成或释放的作用域不会重复回滚。
    /// </remarks>
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        var cleanup = BeginDispose();
        return new ValueTask(DisposeAsyncCore(cleanup));
    }

    /// <summary>
    /// 异步释放工作单元资源并完成状态清理。
    /// </summary>
    /// <param name="cleanup">释放开始时捕获的资源和回滚状态。</param>
    /// <remarks>
    /// 按快照决定是否先回滚；收集清理异常后统一传播。
    /// </remarks>
    private async Task DisposeAsyncCore(DisposeSnapshot cleanup)
    {
        var errors = new List<Exception>();
        if (cleanup.RollbackResources)
            await RollbackResourcesAsync(cleanup.Resources, errors).ConfigureAwait(false);
        await DisposeResourcesAsync(cleanup.Resources, errors).ConfigureAwait(false);
        FinishDispose();
        ThrowCollected(errors);
    }

    /// <summary>
    /// 原子地开始工作单元释放并捕获清理快照。
    /// </summary>
    /// <returns>记录待处理资源、回滚标记和已释放状态的释放快照。</returns>
    /// <remarks>
    /// 已释放或已有释放过程时返回标记为已释放且不再回滚资源的快照。
    /// </remarks>
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

    /// <summary>
    /// 将工作单元作用域标记为已释放。
    /// </summary>
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

    /// <summary>
    /// 确认工作单元没有仍活动的参与者或资源工厂。
    /// </summary>
    /// <remarks>
    /// 参与者未退出或资源工厂尚未完成时抛出异常。
    /// </remarks>
    private void EnsureNoActiveHandles()
    {
        if (_participants != 0)
            throw new InvalidOperationException("仍有未退出的参与式工作单元范围。");
        if (_pendingResourceFactories != 0)
            throw new InvalidOperationException("仍有工作单元资源正在登记。");
        if (_resourceOrder.Any(resource => resource.HasActiveLease))
            throw new InvalidOperationException("仍有 SQL 执行或 reader 持有工作单元资源租约。");
    }

    /// <summary>
    /// 验证工作单元仍允许当前执行流进行操作。
    /// </summary>
    /// <remarks>
    /// 保存阶段只允许保存处理器执行流访问；不允许访问时标记仅回滚并抛出异常。
    /// </remarks>
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

    /// <summary>
    /// 验证并复制工作单元作用域选项。
    /// </summary>
    /// <param name="options">待验证并复制的选项。</param>
    /// <returns>经工作单元管理器校验后复制的选项。</returns>
    private static UnitOfWorkScopeOptions ValidateAndClone(UnitOfWorkScopeOptions options) =>
        UnitOfWorkScopeManager.CloneAndValidateOptions(options);

    /// <summary>
    /// 复制工作单元作用域的隔离级别和超时选项。
    /// </summary>
    /// <param name="options">待复制的工作单元选项。</param>
    /// <returns>复制事务模式、隔离级别和超时值后的选项。</returns>
    private static UnitOfWorkScopeOptions Clone(UnitOfWorkScopeOptions options) => new()
    {
        IsTransactional = options.IsTransactional,
        IsolationLevel = options.IsolationLevel,
        Timeout = options.Timeout
    };

    /// <summary>
    /// 从失败资源开始按逆序回滚未提交资源。
    /// </summary>
    /// <param name="resources">需要处理的资源顺序列表。</param>
    /// <param name="failedIndex">发生失败的资源索引，回滚从该位置向前执行。</param>
    /// <param name="errors">接收回滚期间出现的异常。</param>
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

    /// <summary>
    /// 异步从失败资源开始按逆序回滚未提交资源。
    /// </summary>
    /// <param name="resources">需要处理的资源顺序列表。</param>
    /// <param name="failedIndex">发生失败的资源索引，回滚从该位置向前执行。</param>
    /// <param name="errors">接收回滚期间出现的异常。</param>
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

    /// <summary>
    /// 按逆序回滚仍可回滚的工作单元资源。
    /// </summary>
    /// <param name="resources">需要处理的资源列表。</param>
    /// <param name="errors">接收回滚期间出现的异常。</param>
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

    /// <summary>
    /// 异步按逆序回滚仍可回滚的工作单元资源。
    /// </summary>
    /// <param name="resources">需要处理的资源列表。</param>
    /// <param name="errors">接收回滚期间出现的异常。</param>
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

    /// <summary>
    /// 按逆序释放工作单元资源并收集异常。
    /// </summary>
    /// <param name="resources">需要释放的资源列表。</param>
    /// <param name="errors">接收释放期间出现的异常。</param>
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

    /// <summary>
    /// 异步按逆序释放工作单元资源并收集异常。
    /// </summary>
    /// <param name="resources">需要释放的资源列表。</param>
    /// <param name="errors">接收释放期间出现的异常。</param>
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

    /// <summary>
    /// 为资源操作异常添加工作单元和资源上下文。
    /// </summary>
    /// <param name="exception">需要补充诊断信息的异常。</param>
    /// <param name="entry">发生异常的资源条目。</param>
    /// <param name="index">资源在工作单元中的顺序位置。</param>
    /// <param name="outcome">资源操作结果分类。</param>
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

    /// <summary>
    /// 在原始提交异常之外合并资源清理异常。
    /// </summary>
    /// <param name="primary">提交或工作单元操作的原始异常。</param>
    /// <param name="cleanupErrors">回滚或释放期间收集的异常。</param>
    /// <remarks>
    /// 没有清理异常时保留原始异常的抛出信息；含清理异常时按调用阶段封装。
    /// </remarks>
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

    /// <summary>
    /// 按数量传播已收集的资源异常。
    /// </summary>
    /// <param name="errors">等待传播的异常集合。</param>
    /// <remarks>
    /// 一个异常时保留其抛出信息；多个异常时按实现使用聚合异常。
    /// </remarks>
    private static void ThrowCollected(ICollection<Exception> errors)
    {
        if (errors.Count == 0)
            return;
        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors.First()).Throw();
        throw new AggregateException(errors);
    }

    /// <summary>
    /// 工作单元释放前的资源快照。
    /// </summary>
    private sealed class DisposeSnapshot
    {
        /// <summary>
        /// 初始化一个 <see cref="DisposeSnapshot" /> 类型的实例。
        /// </summary>
        /// <param name="resources">当前工作单元作用域登记的资源集合。</param>
        /// <param name="rollbackResources">指示回滚时是否释放相关资源。</param>
        /// <param name="alreadyDisposed">指示工作单元资源是否已由其他释放路径处理。</param>
        public DisposeSnapshot(List<UnitOfWorkResourceEntry> resources, bool rollbackResources, bool alreadyDisposed)
        {
            Resources = resources;
            RollbackResources = rollbackResources;
            AlreadyDisposed = alreadyDisposed;
        }

        /// <summary>
        /// 获取需要释放的工作单元资源快照。
        /// </summary>
        public List<UnitOfWorkResourceEntry> Resources { get; }
        /// <summary>
        /// 获取需要回滚的未完成资源快照。
        /// </summary>
        public bool RollbackResources { get; }
        /// <summary>
        /// 获取资源是否已经由其他释放路径处理。
        /// </summary>
        public bool AlreadyDisposed { get; }
    }

    /// <summary>
    /// 工作单元完成阶段使用的取消令牌组合。
    /// </summary>
    private readonly struct CompletionCancellation
    {
        /// <summary>
        /// 初始化一个 <see cref="CompletionCancellation" /> 类型的实例。
        /// </summary>
        /// <param name="token">用于取消操作的令牌。</param>
        /// <param name="source">当前操作读取内容或状态的数据源。</param>
        public CompletionCancellation(CancellationToken token, CancellationTokenSource source)
        {
            Token = token;
            Source = source;
        }

        /// <summary>
        /// 获取完成阶段使用的取消令牌。
        /// </summary>
        public CancellationToken Token { get; }
        /// <summary>
        /// 获取组合取消令牌的来源。
        /// </summary>
        public CancellationTokenSource Source { get; }
    }
}

/// <summary>
/// 参与 root 工作单元但不拥有资源提交权的嵌套范围。
/// </summary>
internal sealed class UnitOfWorkScopeParticipant : IUnitOfWorkExecutionScope
{
    /// <summary>
    /// 保存参与者所属的根工作单元作用域。
    /// </summary>
    private readonly UnitOfWorkScope _root;
    /// <summary>
    /// 保存参与者的取消源。
    /// </summary>
    private readonly CancellationTokenSource _cancellationSource;
    /// <summary>
    /// 保存参与者的取消令牌。
    /// </summary>
    private readonly CancellationToken _cancellationToken;
    /// <summary>
    /// 保存参与者对应的工作单元作用域帧。
    /// </summary>
    private UnitOfWorkScopeFrame _frame;
    /// <summary>
    /// 指示参与者是否已完成。
    /// </summary>
    private bool _completed;
    /// <summary>
    /// 指示参与者是否已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkScopeParticipant" /> 类型的实例。
    /// </summary>
    /// <param name="root">当前对象图、工作单元或文件系统的根对象或根路径。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <param name="cancellationSource">用于控制操作取消的令牌源。</param>
    public UnitOfWorkScopeParticipant(UnitOfWorkScope root, CancellationToken cancellationToken,
        CancellationTokenSource cancellationSource)
    {
        _root = root;
        _cancellationToken = cancellationToken;
        _cancellationSource = cancellationSource;
    }

    /// <inheritdoc />
    public Guid Id => _root.Id;
    /// <inheritdoc />
    public UnitOfWorkScopeOptions Options => new()
    {
        IsTransactional = _root.Options.IsTransactional,
        IsolationLevel = _root.Options.IsolationLevel,
        Timeout = _root.Options.Timeout
    };
    /// <inheritdoc />
    public CancellationToken CancellationToken => _cancellationToken;
    /// <inheritdoc />
    public bool IsCompleted => _root.IsCompleted;
    /// <inheritdoc />
    public bool IsActive => !_completed && !_disposed && _root.IsActive;
    /// <inheritdoc />
    public bool IsRollbackOnly => _root.IsRollbackOnly;
    /// <inheritdoc />
    public bool IsDisposed => _disposed;

    /// <summary>
    /// 关联由管理器创建的工作单元栈帧。
    /// </summary>
    /// <param name="frame">要附加到参与范围的栈帧。</param>
    public void AttachFrame(UnitOfWorkScopeFrame frame) => _frame = frame;

    /// <inheritdoc />
    public TResource GetOrAddResource<TResource>(object key, Func<TResource> factory)
        where TResource : class, IUnitOfWorkResource => _root.GetOrAddResource(key, factory);

    /// <inheritdoc />
    public IDisposable AcquireExecutionLease(object resourceKey) => _root.AcquireExecutionLease(resourceKey);
    /// <inheritdoc />
    public void OnResourceCommitted(object resourceKey, Func<Task> callback) => _root.OnResourceCommitted(resourceKey, callback);
    /// <inheritdoc />
    public void OnCompleted(Func<Task> callback) => _root.OnCompleted(callback);
    /// <inheritdoc />
    public void SetRollbackOnly() => _root.SetRollbackOnly();

    /// <inheritdoc />
    public void Complete()
    {
        EnsureOpen();
        ThrowIfCancellationRequested();
        _completed = true;
    }

    /// <inheritdoc />
    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        EnsureOpen();
        ThrowIfCancellationRequested(cancellationToken);
        Complete();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Rollback()
    {
        EnsureOpen();
        _root.SetRollbackOnly();
        _completed = true;
    }

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        Rollback();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    /// <summary>
    /// 验证参与式工作单元范围仍可使用。
    /// </summary>
    /// <remarks>
    /// 范围已完成或释放时抛出 InvalidOperationException。
    /// </remarks>
    private void EnsureOpen()
    {
        if (_disposed || _completed)
            throw new InvalidOperationException("参与式工作单元范围已完成或释放。");
    }

    /// <summary>
    /// 使用参与范围的有效取消令牌检查取消请求。
    /// </summary>
    /// <param name="additionalToken">附加取消令牌；范围自身令牌尚未取消时使用。</param>
    /// <remarks>
    /// 任一令牌已取消时传播 OperationCanceledException。
    /// </remarks>
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

/// <summary>
/// 标识工作单元执行作用域的事务处理阶段。
/// </summary>
internal enum UnitOfWorkScopeState
{
    /// <summary>
    /// 作用域可接受操作并继续收集变更。
    /// </summary>
    Active,
    /// <summary>
    /// 作用域正在保存当前变更。
    /// </summary>
    Saving,
    /// <summary>
    /// 保存结束后正在完成事务与回调。
    /// </summary>
    Completing,
    /// <summary>
    /// 作用域已成功完成。
    /// </summary>
    Completed,
    /// <summary>
    /// 作用域正在回滚未完成的事务。
    /// </summary>
    RollingBack,
    /// <summary>
    /// 作用域已完成回滚。
    /// </summary>
    RolledBack,
    /// <summary>
    /// 保存、提交或回滚过程发生故障。
    /// </summary>
    Faulted,
    /// <summary>
    /// 作用域已释放，不能继续使用。
    /// </summary>
    Disposed
}

/// <summary>
/// 保存一个 root 的资源、租约和逐资源成功通知。
/// </summary>
internal sealed class UnitOfWorkResourceEntry
{
    /// <summary>
    /// 保存延迟创建的工作单元资源。
    /// </summary>
    private readonly Lazy<IUnitOfWorkResource> _resource;
    /// <summary>
    /// 保存资源提交后执行的回调集合。
    /// </summary>
    private readonly List<Func<Task>> _committedCallbacks = new();
    /// <summary>
    /// 记录资源工厂是否已完成。
    /// </summary>
    private int _factoryFinished;
    /// <summary>
    /// 记录资源租约是否活动。
    /// </summary>
    private int _leaseActive;
    /// <summary>
    /// 记录资源释放是否已开始。
    /// </summary>
    private int _disposeStarted;
    /// <summary>
    /// 记录资源是否已尝试回滚。
    /// </summary>
    private int _rollbackAttempted;
    /// <summary>
    /// 记录资源当前提交状态。
    /// </summary>
    private int _commitState;

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkResourceEntry" /> 类型的实例。
    /// </summary>
    /// <param name="resourceType">当前工作单元资源的运行时类型。</param>
    /// <param name="factory">用于创建实例或其依赖项的工厂。</param>
    public UnitOfWorkResourceEntry(Type resourceType, Func<IUnitOfWorkResource> factory)
    {
        ResourceType = resourceType;
        _resource = new Lazy<IUnitOfWorkResource>(() =>
            factory() ?? throw new InvalidOperationException("工作单元资源工厂返回了 null。"),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// 获取工作单元资源的契约类型。
    /// </summary>
    public Type ResourceType { get; }
    /// <summary>
    /// 获取当前资源实例。
    /// </summary>
    public IUnitOfWorkResource Resource => GetResource();
    /// <summary>
    /// 获取或设置资源所属工作单元标识。
    /// </summary>
    public Guid UnitOfWorkId { get; set; }
    /// <summary>
    /// 获取资源是否已经完成创建。
    /// </summary>
    public bool IsResourceCreated => _resource.IsValueCreated;
    /// <summary>
    /// 获取资源是否已经确认提交。
    /// </summary>
    public bool IsCommitted => Volatile.Read(ref _commitState) == 1;
    /// <summary>
    /// 获取资源提交结果是否未知。
    /// </summary>
    public bool IsCommitUnknown => Volatile.Read(ref _commitState) == 2;
    /// <summary>
    /// 获取资源是否已经回滚。
    /// </summary>
    public bool IsRolledBack => Volatile.Read(ref _commitState) == 3;
    /// <summary>
    /// 获取资源是否仍被活动租约持有。
    /// </summary>
    public bool HasActiveLease => Volatile.Read(ref _leaseActive) != 0;
    /// <summary>
    /// 获取资源确认提交后执行的回调集合。
    /// </summary>
    public IReadOnlyList<Func<Task>> CommittedCallbacks => _committedCallbacks;

    /// <summary>
    /// 获取或创建工作单元资源。
    /// </summary>
    /// <returns>由惰性资源工厂创建或已缓存的工作单元资源。</returns>
    /// <remarks>
    /// 资源工厂执行完成前会等待其结果，并返回共享资源实例。
    /// </remarks>
    public IUnitOfWorkResource GetResource()
    {
        var resource = _resource.Value;
        return resource;
    }

    /// <summary>
    /// 原子地尝试将资源工厂标记为已完成。
    /// </summary>
    /// <returns>本次调用首次标记资源工厂完成时为 true；此前已完成时为 false。</returns>
    /// <remarks>
    /// 首次将状态从未完成改为已完成时返回 true，否则返回 false。
    /// </remarks>
    public bool TryFinishFactory() => Interlocked.Exchange(ref _factoryFinished, 1) == 0;
    /// <summary>
    /// 原子地获取资源独占执行租约。
    /// </summary>
    /// <returns>本次调用成功取得资源租约时为 true；已有租约时为 false。</returns>
    /// <remarks>
    /// 租约空闲并成功获取时返回 true；已有调用持有租约时返回 false。
    /// </remarks>
    public bool TryAcquireLease() => Interlocked.CompareExchange(ref _leaseActive, 1, 0) == 0;
    /// <summary>
    /// 释放资源独占执行租约。
    /// </summary>
    public void ReleaseLease() => Interlocked.Exchange(ref _leaseActive, 0);
    /// <summary>
    /// 登记资源提交成功后的异步回调。
    /// </summary>
    /// <param name="callback">物理资源提交后执行的回调。</param>
    public void AddCommittedCallback(Func<Task> callback) => _committedCallbacks.Add(callback);
    /// <summary>
    /// 将资源事务状态标记为已提交。
    /// </summary>
    public void MarkCommitted() => Volatile.Write(ref _commitState, 1);
    /// <summary>
    /// 将资源事务状态标记为结果未知。
    /// </summary>
    public void MarkCommitUnknown() => Volatile.Write(ref _commitState, 2);
    /// <summary>
    /// 原子地尝试取得资源回滚处理权。
    /// </summary>
    /// <returns>本次调用首次启动回滚时为 true；回滚已启动时为 false。</returns>
    /// <remarks>
    /// 首次登记回滚尝试时返回 true；已有回滚处理已启动时返回 false。
    /// </remarks>
    public bool TryBeginRollback() => Interlocked.CompareExchange(ref _rollbackAttempted, 1, 0) == 0;
    /// <summary>
    /// 将资源状态标记为已回滚。
    /// </summary>
    /// <remarks>
    /// 若提交结果未知，则保留未知状态，不覆盖提交状态记录。
    /// </remarks>
    public void MarkRolledBack()
    {
        if (!IsCommitUnknown)
            Volatile.Write(ref _commitState, 3);
    }
    /// <summary>
    /// 原子地尝试开始释放资源。
    /// </summary>
    /// <returns>本次调用首次启动资源释放时为 true；释放已启动时为 false。</returns>
    /// <remarks>
    /// 首次标记开始释放时返回 true；释放已经开始时返回 false。
    /// </remarks>
    public bool TryBeginDispose() => Interlocked.Exchange(ref _disposeStarted, 1) == 0;
}

/// <summary>
/// 持有工作单元资源并在释放时结束租约。
/// </summary>
internal sealed class UnitOfWorkResourceLease : IDisposable
{
    /// <summary>
    /// 保存资源租约对应的资源条目。
    /// </summary>
    private UnitOfWorkResourceEntry _entry;

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkResourceLease" /> 类型的实例。
    /// </summary>
    /// <param name="entry">当前工作单元资源登记项。</param>
    public UnitOfWorkResourceLease(UnitOfWorkResourceEntry entry) => _entry = entry;

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref _entry, null)?.ReleaseLease();
}
