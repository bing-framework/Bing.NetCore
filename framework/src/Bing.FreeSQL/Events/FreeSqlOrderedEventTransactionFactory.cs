using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;

namespace Bing.FreeSQL.Events;

/// <summary>
/// 创建不接管调用方工作单元的 FreeSQL 有序事件事务。
/// </summary>
public sealed class FreeSqlOrderedEventTransactionFactory : IOrderedEventTransactionFactory
{
    /// <summary>
    /// 宿主拥有的 ORM。
    /// </summary>
    private readonly IFreeSql _orm;

    /// <summary>
    /// 初始化一个 <see cref="FreeSqlOrderedEventTransactionFactory" /> 类型的实例。
    /// </summary>
    /// <param name="orm">宿主拥有的 ORM。</param>
    public FreeSqlOrderedEventTransactionFactory(IFreeSql orm) =>
        _orm = orm ?? throw new ArgumentNullException(nameof(orm));

    /// <inheritdoc />
    public Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unitOfWork = _orm.CreateUnitOfWork();
        try
        {
            var transaction = unitOfWork.GetOrBeginTransaction();
            return Task.FromResult<IOrderedEventTransaction>(new TransactionScope(unitOfWork, transaction));
        }
        catch (Exception exception)
        {
            try
            {
                unitOfWork.Dispose();
            }
            catch (Exception cleanupException)
            {
                exception.Data["Bing.TransactionCleanupException"] = cleanupException;
            }
            throw;
        }
    }

    /// <summary>
    /// 管理当前创建的工作单元。
    /// </summary>
    private sealed class TransactionScope : IOrderedEventTransactionState
    {
        /// <summary>
        /// 当前创建的工作单元。
        /// </summary>
        private readonly global::FreeSql.IUnitOfWork _unitOfWork;
        /// <summary>
        /// 事务是否已经结束。
        /// </summary>
        private bool _finished;
        /// <summary>
        /// 当前能够确认的事务结果。
        /// </summary>
        private TransactionOutcome _outcome;
        /// <summary>
        /// 同步释放任务的创建。
        /// </summary>
        private readonly object _disposeGate = new();
        /// <summary>
        /// 唯一的资源清理任务。
        /// </summary>
        private Task _disposeTask;

        /// <summary>
        /// 初始化一个 <see cref="TransactionScope" /> 类型的实例。
        /// </summary>
        internal TransactionScope(global::FreeSql.IUnitOfWork unitOfWork, DbTransaction transaction)
        {
            _unitOfWork = unitOfWork;
            Transaction = transaction;
        }

        /// <inheritdoc />
        public DbTransaction Transaction { get; }

        /// <inheritdoc />
        public TransactionOutcome Outcome => _outcome;

        /// <inheritdoc />
        public void ActivateAmbient() => ThrowIfClosed();

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            if (_finished)
                throw new InvalidOperationException("事务已经结束，不能再次提交。");
            cancellationToken.ThrowIfCancellationRequested();
            _unitOfWork.Commit();
            _finished = true;
            _outcome = TransactionOutcome.Committed;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            cancellationToken.ThrowIfCancellationRequested();
            if (!_finished)
            {
                _unitOfWork.Rollback();
                _finished = true;
                _outcome = TransactionOutcome.RolledBack;
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ResetBusinessStateAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            if (_finished)
                throw new InvalidOperationException("事务已经结束，不能重置业务状态。");
            cancellationToken.ThrowIfCancellationRequested();
            // 保存点只回滚数据库，必须丢弃 FreeSQL 变更报告中的待保存实体。
            _unitOfWork.EntityChangeReport?.Report?.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            lock (_disposeGate)
                return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }

        /// <summary>
        /// 清理本次工作单元并保留所有释放异常。
        /// </summary>
        private Task DisposeCoreAsync()
        {
            Exception failure = null;
            try
            {
                if (!_finished)
                {
                    _unitOfWork.Rollback();
                    _outcome = TransactionOutcome.RolledBack;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            try
            {
                _unitOfWork.Dispose();
            }
            catch (Exception exception)
            {
                if (failure != null)
                    return Task.FromException(new AggregateException(failure, exception));
                return Task.FromException(exception);
            }
            if (failure != null)
                return Task.FromException(failure);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 拒绝释放开始后的事务操作。
        /// </summary>
        private void ThrowIfClosed()
        {
            lock (_disposeGate)
            {
                if (_disposeTask != null)
                    throw new ObjectDisposedException(nameof(TransactionScope));
            }
        }
    }
}
