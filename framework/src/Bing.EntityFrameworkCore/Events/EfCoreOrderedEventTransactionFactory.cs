using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Data.Transaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bing.EntityFrameworkCore.Events;

/// <summary>
/// 为当前 EFCore 作用域创建有序事件事务。
/// </summary>
/// <typeparam name="TContext">业务数据库上下文类型。</typeparam>
public sealed class EfCoreOrderedEventTransactionFactory<TContext> : IOrderedEventTransactionFactory
    where TContext : DbContext
{
    /// <summary>
    /// 当前作用域的数据库上下文。
    /// </summary>
    private readonly TContext _context;

    /// <summary>
    /// 初始化一个 <see cref="EfCoreOrderedEventTransactionFactory{TContext}" /> 类型的实例。
    /// </summary>
    /// <param name="context">当前作用域的业务数据库上下文。</param>
    public EfCoreOrderedEventTransactionFactory(TContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
            throw new InvalidOperationException("有序事件处理不能接管调用方已开启的事务。");
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new TransactionScope(_context, transaction);
    }

    /// <summary>
    /// 仅拥有本次 EFCore 事务的生命周期。
    /// </summary>
    private sealed class TransactionScope : IOrderedEventTransactionState
    {
        /// <summary>
        /// 当前事务。
        /// </summary>
        private readonly IDbContextTransaction _transaction;
        /// <summary>
        /// 用于清理保存点外跟踪状态的数据库上下文。
        /// </summary>
        private readonly DbContext _context;
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
        internal TransactionScope(IDbContextTransaction transaction) : this(null, transaction)
        {
        }

        /// <summary>
        /// 初始化一个 <see cref="TransactionScope" /> 类型的实例。
        /// </summary>
        /// <remarks>
        /// 初始化一个带上下文状态清理能力的事务作用域。
        /// </remarks>
        /// <param name="context">当前数据库上下文。</param>
        /// <param name="transaction">当前数据库事务。</param>
        internal TransactionScope(DbContext context, IDbContextTransaction transaction)
        {
            _context = context;
            _transaction = transaction;
        }

        /// <inheritdoc />
        public DbTransaction Transaction => _transaction.GetDbTransaction();

        /// <inheritdoc />
        public TransactionOutcome Outcome => _outcome;

        /// <inheritdoc />
        public void ActivateAmbient() => ThrowIfClosed();

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            if (_finished)
                throw new InvalidOperationException("事务已经结束，不能再次提交。");
            await _transaction.CommitAsync(cancellationToken);
            _finished = true;
            _outcome = TransactionOutcome.Committed;
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            if (_finished)
                return;
            await _transaction.RollbackAsync(cancellationToken);
            _finished = true;
            _outcome = TransactionOutcome.RolledBack;
        }

        /// <inheritdoc />
        public Task ResetBusinessStateAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            if (_finished)
                throw new InvalidOperationException("事务已经结束，不能重置业务状态。");
            cancellationToken.ThrowIfCancellationRequested();
            // 保存点只回滚数据库，必须同时丢弃上下文中的 Added、Modified 和 Unchanged 跟踪项。
            _context?.ChangeTracker.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            lock (_disposeGate)
                return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }

        /// <summary>
        /// 清理本次事务并保留所有释放异常。
        /// </summary>
        private async Task DisposeCoreAsync()
        {
            Exception failure = null;
            try
            {
                if (!_finished)
                {
                    await _transaction.RollbackAsync(CancellationToken.None);
                    _outcome = TransactionOutcome.RolledBack;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            try
            {
                await _transaction.DisposeAsync();
            }
            catch (Exception exception)
            {
                if (failure != null)
                    throw new AggregateException(failure, exception);
                throw;
            }
            if (failure != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
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
