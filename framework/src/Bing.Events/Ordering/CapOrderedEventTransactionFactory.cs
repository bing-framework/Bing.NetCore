using System.Data.Common;
using System.Runtime.ExceptionServices;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Transport;

namespace Bing.Events.Ordering;

/// <summary>
/// 将有序数据库事务与当前作用域的 CAP 发布缓冲绑定。
/// </summary>
/// <remarks>
/// 底层工厂始终拥有数据库事务。调用方必须在等待开启或业务重置完成后，在自身执行上下文同步调用 ActivateAmbient。
/// 提交后的 Flush 使用 CAP 原生缓冲算法；本适配器会等待 dispatcher 返回的入队任务完成，但不表示 RabbitMQ 已经确认消息。
/// </remarks>
public sealed class CapOrderedEventTransactionFactory : IOrderedEventTransactionFactory
{
    /// <summary>
    /// 获取数据库事务的唯一资源所有者。
    /// </summary>
    private readonly IOrderedEventTransactionFactory _inner;

    /// <summary>
    /// 保存当前执行上下文的 CAP 事务。
    /// </summary>
    private readonly ICapPublisher _publisher;

    /// <summary>
    /// 接收物理提交后的原生 CAP 缓冲。
    /// </summary>
    private readonly IDispatcher _dispatcher;

    /// <summary>
    /// 清理宿主拥有的业务事务外状态。
    /// </summary>
    private readonly Func<CancellationToken, Task> _resetBusinessState;

    /// <summary>
    /// 标识本工厂是否正在开启或拥有未释放的事务。
    /// </summary>
    private int _active;

    /// <summary>
    /// 初始化一个 <see cref="CapOrderedEventTransactionFactory" /> 类型的实例。
    /// </summary>
    /// <param name="inner">拥有数据库事务生命周期的底层工厂。</param>
    /// <param name="publisher">当前作用域的 CAP 发布器。</param>
    /// <param name="dispatcher">CAP 原生消息队列入口。</param>
    /// <param name="resetBusinessState">可选的宿主业务状态清理回调，不应捕获根容器。</param>
    public CapOrderedEventTransactionFactory(IOrderedEventTransactionFactory inner, ICapPublisher publisher,
        IDispatcher dispatcher, Func<CancellationToken, Task> resetBusinessState = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _resetBusinessState = resetBusinessState;
    }

    /// <inheritdoc />
    public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_publisher.Transaction.Value != null)
            throw new InvalidOperationException("有序事件处理不能接管已有的 CAP 事务。");
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            throw new InvalidOperationException("当前工厂已有正在开启或尚未释放的事务。");

        IOrderedEventTransaction transaction = null;
        try
        {
            transaction = await _inner.BeginAsync(cancellationToken).ConfigureAwait(false);
            if (transaction == null)
                throw new InvalidOperationException("底层事务工厂未返回事务。");
            return new TransactionScope(transaction, _publisher, _dispatcher, _resetBusinessState,
                () => Volatile.Write(ref _active, 0));
        }
        catch (Exception exception)
        {
            var errors = new List<Exception>();
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }
                try
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }
            }
            Volatile.Write(ref _active, 0);
            if (errors.Count != 0)
                exception.Data["Bing.TransactionCleanupException"] = errors.Count == 1
                    ? errors[0]
                    : new AggregateException(errors);
            throw;
        }
    }

    /// <summary>
    /// 管理一次数据库事务与可替换的 CAP 缓冲。
    /// </summary>
    private sealed class TransactionScope : IOrderedEventTransactionState
    {
        /// <summary>
        /// 获取数据库事务的唯一资源所有者。
        /// </summary>
        private readonly IOrderedEventTransaction _inner;

        /// <summary>
        /// 保存调用方执行上下文的 CAP 事务。
        /// </summary>
        private readonly ICapPublisher _publisher;

        /// <summary>
        /// 创建每次重置后的原生 CAP 缓冲。
        /// </summary>
        private readonly IDispatcher _dispatcher;

        /// <summary>
        /// 清理宿主拥有的业务状态。
        /// </summary>
        private readonly Func<CancellationToken, Task> _resetBusinessState;

        /// <summary>
        /// 在唯一清理结束后允许工厂再次开启事务。
        /// </summary>
        private readonly Action _release;

        /// <summary>
        /// 同步当前缓冲、激活及唯一释放任务。
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        /// 串行化物理事务操作与最终清理。
        /// </summary>
        private readonly SemaphoreSlim _operations = new(1, 1);

        /// <summary>
        /// 记录本作用域历次缓冲，识别其他执行上下文中的旧引用。
        /// </summary>
        private readonly List<OwnedCapTransaction> _owned = new();

        /// <summary>
        /// 获取最终提交时唯一允许发送的缓冲。
        /// </summary>
        private OwnedCapTransaction _current;

        /// <summary>
        /// 标识物理事务已确认结束。
        /// </summary>
        private bool _finished;

        /// <summary>
        /// 保存已确认的物理事务结果。
        /// </summary>
        private TransactionOutcome _outcome;

        /// <summary>
        /// 阻止重复调用物理提交。
        /// </summary>
        private bool _commitAttempted;

        /// <summary>
        /// 阻止清理再次调用已经尝试的物理回滚。
        /// </summary>
        private bool _rollbackAttempted;

        /// <summary>
        /// 标识公开释放入口已经关闭业务操作。
        /// </summary>
        private bool _closed;

        /// <summary>
        /// 保存所有释放调用共同等待的清理任务。
        /// </summary>
        private Task _disposeTask;

        /// <summary>
        /// 初始化一个 <see cref="TransactionScope" /> 类型的实例。
        /// </summary>
        /// <param name="inner">拥有物理数据库事务的底层作用域。</param>
        /// <param name="publisher">当前作用域的 CAP 发布器。</param>
        /// <param name="dispatcher">CAP 原生消息队列入口。</param>
        /// <param name="resetBusinessState">可选的宿主状态清理回调。</param>
        /// <param name="release">结束工厂占用的回调。</param>
        internal TransactionScope(IOrderedEventTransaction inner, ICapPublisher publisher, IDispatcher dispatcher,
            Func<CancellationToken, Task> resetBusinessState, Action release)
        {
            _inner = inner;
            _publisher = publisher;
            _dispatcher = dispatcher;
            _resetBusinessState = resetBusinessState;
            _release = release;
            Transaction = inner.Transaction ?? throw new InvalidOperationException("底层事务未提供数据库事务。");
            _current = CreateCapTransaction();
        }

        /// <inheritdoc />
        public DbTransaction Transaction { get; }

        /// <inheritdoc />
        public TransactionOutcome Outcome
        {
            get
            {
                lock (_gate)
                    return _outcome;
            }
        }

        /// <inheritdoc />
        public void ActivateAmbient()
        {
            lock (_gate)
            {
                ThrowIfClosed();
                if (_finished)
                    throw new InvalidOperationException("事务已经结束，不能再次激活 CAP 缓冲。");
                var ambient = _publisher.Transaction.Value;
                if (ambient != null && !Owns(ambient))
                    throw new InvalidOperationException("当前 CAP 事务已被外部替换，不能覆盖。");
                _publisher.Transaction.Value = _current;
            }
        }

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_gate)
                {
                    ThrowIfClosed();
                    if (_finished || _commitAttempted || _rollbackAttempted)
                        throw new InvalidOperationException("事务已经结束或尝试结束，不能再次提交。");
                    _commitAttempted = true;
                }
                try
                {
                    await _inner.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    var outcome = _inner is IOrderedEventTransactionState state
                        ? state.Outcome
                        : exception.GetTransactionOutcome();
                    SetOutcome(outcome, outcome != TransactionOutcome.Unknown);
                    exception.SetTransactionOutcome(outcome);
                    throw;
                }
                SetOutcome(TransactionOutcome.Committed, true);
                try
                {
                    await _current.FlushNativeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    exception.SetTransactionOutcome(TransactionOutcome.Committed);
                    throw;
                }
            }
            finally
            {
                _operations.Release();
            }
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_gate)
                    ThrowIfClosed();
                await RollbackCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _operations.Release();
            }
        }

        /// <inheritdoc />
        public async Task ResetBusinessStateAsync(CancellationToken cancellationToken = default)
        {
            await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_gate)
                {
                    ThrowIfClosed();
                    if (_finished || _commitAttempted || _rollbackAttempted)
                        throw new InvalidOperationException("事务已经尝试结束，不能重置业务状态。");
                }
                if (_inner is IOrderedEventTransactionState state)
                    await state.ResetBusinessStateAsync(cancellationToken).ConfigureAwait(false);
                if (_resetBusinessState != null)
                    await _resetBusinessState(cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    _current.Dispose();
                    _current = CreateCapTransaction();
                }
            }
            finally
            {
                _operations.Release();
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            lock (_gate)
            {
                var ambient = _publisher.Transaction.Value;
                if (ambient != null && Owns(ambient))
                    _publisher.Transaction.Value = null;
                if (_disposeTask != null)
                    return new ValueTask(_disposeTask);
                _closed = true;
                var failure = ambient != null && !Owns(ambient)
                    ? new InvalidOperationException("当前 CAP 事务已被外部替换，清理不会覆盖外部事务。")
                    : null;
                return new ValueTask(_disposeTask = DisposeCoreAsync(failure));
            }
        }

        /// <summary>
        /// 创建借用同一物理事务的新 CAP 缓冲。
        /// </summary>
        /// <returns>由当前作用域拥有的空 CAP 缓冲。</returns>
        private OwnedCapTransaction CreateCapTransaction()
        {
            var transaction = new OwnedCapTransaction(this, _dispatcher)
            {
                DbTransaction = Transaction,
                AutoCommit = false
            };
            _owned.Add(transaction);
            return transaction;
        }

        /// <summary>
        /// 判断 ambient 是否属于当前作用域的历次缓冲。
        /// </summary>
        /// <param name="transaction">调用方当前的 CAP 事务。</param>
        /// <returns>属于本作用域时返回 true，否则返回 false。</returns>
        private bool Owns(ICapTransaction transaction) => _owned.Any(value => ReferenceEquals(value, transaction));

        /// <summary>
        /// 至多尝试一次尚未结束的物理回滚。
        /// </summary>
        /// <param name="cancellationToken">物理回滚的取消令牌。</param>
        private async Task RollbackCoreAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_finished || _rollbackAttempted)
                    return;
                cancellationToken.ThrowIfCancellationRequested();
                _rollbackAttempted = true;
            }
            try
            {
                await _inner.RollbackAsync(cancellationToken).ConfigureAwait(false);
                SetOutcome(TransactionOutcome.RolledBack, true);
            }
            catch (Exception exception)
            {
                var outcome = _inner is IOrderedEventTransactionState state
                    ? state.Outcome
                    : exception.GetTransactionOutcome();
                SetOutcome(outcome, outcome != TransactionOutcome.Unknown);
                exception.SetTransactionOutcome(outcome);
                throw;
            }
        }

        /// <summary>
        /// 在 ambient 激活与结束检查使用的同步边界内更新事务结果。
        /// </summary>
        /// <param name="outcome">已确认的事务结果。</param>
        /// <param name="finished">是否禁止后续事务操作。</param>
        private void SetOutcome(TransactionOutcome outcome, bool finished)
        {
            lock (_gate)
            {
                _outcome = outcome;
                _finished = finished;
            }
        }

        /// <summary>
        /// 等待业务操作结束并清理所有自有包装及底层资源。
        /// </summary>
        /// <param name="ambientFailure">同步解绑时发现的外部事务替换错误。</param>
        private async Task DisposeCoreAsync(Exception ambientFailure)
        {
            var errors = new List<Exception>();
            if (ambientFailure != null)
                errors.Add(ambientFailure);
            await _operations.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                try
                {
                    await RollbackCoreAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
                lock (_gate)
                {
                    foreach (var transaction in _owned)
                        transaction.Dispose();
                }
                try
                {
                    await _inner.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }
            finally
            {
                _operations.Release();
                _release();
            }
            if (errors.Count == 1)
                ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1)
                throw new AggregateException(errors);
        }

        /// <summary>
        /// 拒绝释放入口启动后的业务操作。
        /// </summary>
        private void ThrowIfClosed()
        {
            if (_closed)
                throw new ObjectDisposedException(nameof(TransactionScope));
        }
    }

    /// <summary>
    /// 借用物理事务并将 CAP 结束操作转交给有序事务作用域。
    /// </summary>
    private sealed class OwnedCapTransaction : CapTransactionBase
    {
        /// <summary>
        /// 获取物理事务与缓冲的唯一协调者。
        /// </summary>
        private readonly TransactionScope _scope;

        /// <summary>
        /// 收集本次缓冲的异步入队任务。
        /// </summary>
        private readonly TrackingDispatcher _trackingDispatcher;

        /// <summary>
        /// 初始化一个 <see cref="OwnedCapTransaction" /> 类型的实例。
        /// </summary>
        /// <param name="scope">管理提交结果与资源释放的作用域。</param>
        /// <param name="dispatcher">原生 CAP 消息队列入口。</param>
        internal OwnedCapTransaction(TransactionScope scope, IDispatcher dispatcher)
            : this(scope, new TrackingDispatcher(dispatcher))
        {
        }

        /// <summary>
        /// 初始化一个 <see cref="OwnedCapTransaction" /> 类型的实例。
        /// </summary>
        /// <remarks>
        /// 使用事务专属的 dispatcher 跟踪器初始化 CAP 缓冲。
        /// </remarks>
        /// <param name="scope">管理提交结果与资源释放的作用域。</param>
        /// <param name="dispatcher">事务专属的入队任务跟踪器。</param>
        private OwnedCapTransaction(TransactionScope scope, TrackingDispatcher dispatcher)
            : base(dispatcher)
        {
            _scope = scope;
            _trackingDispatcher = dispatcher;
        }

        /// <inheritdoc />
        public override void Commit() => _scope.CommitAsync().GetAwaiter().GetResult();

        /// <inheritdoc />
        public override Task CommitAsync(CancellationToken cancellationToken = default) => _scope.CommitAsync(cancellationToken);

        /// <inheritdoc />
        public override void Rollback() => _scope.RollbackAsync().GetAwaiter().GetResult();

        /// <inheritdoc />
        public override Task RollbackAsync(CancellationToken cancellationToken = default) => _scope.RollbackAsync(cancellationToken);

        /// <inheritdoc />
        public override void Dispose() => DbTransaction = null;

        /// <summary>
        /// 调用 CAP 原生缓冲发送逻辑并等待本次入队任务完成。
        /// </summary>
        internal async Task FlushNativeAsync()
        {
            Exception flushException = null;
            try
            {
                Flush();
            }
            catch (Exception exception)
            {
                flushException = exception;
            }

            try
            {
                await _trackingDispatcher.WaitForCompletionAsync().ConfigureAwait(false);
            }
            catch (Exception dispatchException)
            {
                if (flushException == null)
                    flushException = dispatchException;
                else
                    flushException.Data["Bing.CapDispatchException"] = dispatchException;
            }

            if (flushException != null)
                ExceptionDispatchInfo.Capture(flushException).Throw();
        }
    }

    /// <summary>
    /// 跟踪 CAP 原生 dispatcher 返回的异步入队任务。
    /// </summary>
    private sealed class TrackingDispatcher : IDispatcher
    {
        /// <summary>
        /// 实际的 CAP dispatcher。
        /// </summary>
        private readonly IDispatcher _inner;

        /// <summary>
        /// 保护待完成任务集合。
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>
        /// 本次缓冲已发起的入队任务。
        /// </summary>
        private readonly List<Task> _pending = new List<Task>();

        /// <summary>
        /// 初始化一个 <see cref="TrackingDispatcher" /> 类型的实例。
        /// </summary>
        /// <param name="inner">实际的 CAP dispatcher。</param>
        internal TrackingDispatcher(IDispatcher inner) => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        /// <inheritdoc />
        public ValueTask EnqueueToPublish(MediumMessage message) => Track(_inner.EnqueueToPublish(message));

        /// <inheritdoc />
        public ValueTask EnqueueToExecute(MediumMessage message, ConsumerExecutorDescriptor descriptor) =>
            Track(_inner.EnqueueToExecute(message, descriptor));

        /// <inheritdoc />
        public ValueTask EnqueueToScheduler(MediumMessage message, DateTime executeAt, object payload) =>
            Track(_inner.EnqueueToScheduler(message, executeAt, payload));

        /// <inheritdoc />
        public Task Start(CancellationToken stoppingToken) => _inner.Start(stoppingToken);

        /// <inheritdoc />
        public void Dispose() => _inner.Dispose();

        /// <summary>
        /// 记录 dispatcher 返回的任务并保留原始返回值。
        /// </summary>
        /// <param name="operation">dispatcher 入队操作。</param>
        /// <returns>原始入队操作。</returns>
        private ValueTask Track(ValueTask operation)
        {
            // CAP 的常见实现会同步完成入队；不需要为已完成的 ValueTask 创建并保留 Task。
            if (operation.IsCompletedSuccessfully)
                return operation;
            var task = operation.AsTask();
            lock (_gate)
                _pending.Add(task);
            return operation;
        }

        /// <summary>
        /// 等待当前缓冲已经发起的全部入队任务。
        /// </summary>
        internal async Task WaitForCompletionAsync()
        {
            Task[] pending;
            lock (_gate)
            {
                pending = _pending.ToArray();
                _pending.Clear();
            }
            if (pending.Length != 0)
            {
                try
                {
                    await Task.WhenAll(pending).ConfigureAwait(false);
                }
                catch
                {
                    // await Task.WhenAll 只会抛出一个异常；把所有异步入队故障保留下来，避免提交后的诊断缺项。
                    var errors = pending
                        .Where(task => task.IsFaulted && task.Exception != null)
                        .SelectMany(task => task.Exception.InnerExceptions)
                        .ToArray();
                    if (errors.Length == 0)
                        throw;
                    if (errors.Length == 1)
                        ExceptionDispatchInfo.Capture(errors[0]).Throw();
                    throw new AggregateException("CAP 异步入队操作有多个失败。", errors);
                }
            }
        }
    }
}
