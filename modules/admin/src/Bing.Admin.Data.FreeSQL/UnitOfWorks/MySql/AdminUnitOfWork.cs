using System;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Transaction;
using Bing.FreeSQL;
using DotNetCore.CAP;
using DotNetCore.CAP.Transport;

namespace Bing.Admin.Data.UnitOfWorks.MySql
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public class AdminUnitOfWork : Bing.Uow.UnitOfWork, IAdminUnitOfWork
    {
        /// <summary>
        /// 获取或设置测试用 CAP 事务创建器。
        /// </summary>
        internal Func<IDispatcher, ICapTransaction> CapTransactionFactory { get; set; }

        /// <summary>
        /// 初始化一个 <see cref="AdminUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="orm">FreeSql</param>
        /// <param name="serviceProvider">服务提供器</param>
        public AdminUnitOfWork(FreeSqlWrapper orm, IServiceProvider serviceProvider = null) : base(orm, serviceProvider)
        {
        }

        /// <inheritdoc />
        public override int SaveChanges()
        {
            return base.SaveChanges();
        }

        /// <summary>
        /// 异步保存更改
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        protected override int TransactionCommit(ITransactionActionManager transactionActionManager) =>
            TransactionCommit(transactionActionManager, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// 手工创建事务提交
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="cancellationToken">取消令牌</param>
        protected override async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publisher = LazyServiceProvider.LazyGetService<ICapPublisher>();
            var previousTransaction = publisher.Transaction.Value;
            ICapTransaction capTransaction = null;
            var databaseCommitted = false;
            var commitStarted = false;
            var outcome = TransactionOutcome.Unknown;
            Exception failure = null;
            var result = 0;
            try
            {
                capTransaction = CapTransactionFactory == null
                    ? UnitOfWork.BeginTransaction(publisher, autoCommit: false)
                    : CapUnitOfWorkExtensions.BeginTransaction(UnitOfWork, publisher, CapTransactionFactory,
                        autoCommit: false);
                await transactionActionManager.CommitAsync(
                    publisher.Transaction.Value.DbTransaction as IDbTransaction);
                cancellationToken.ThrowIfCancellationRequested();
                result = await SaveChangesCoreAsync(cancellationToken);
                commitStarted = true;
                UnitOfWork.Commit(capTransaction, () => databaseCommitted = true);
            }
            catch (Exception exception)
            {
                if (databaseCommitted)
                    failure = exception;
                else if (capTransaction == null)
                    failure = exception;
                else
                {
                    failure = CapUnitOfWorkExtensions.RollbackBoth(exception,
                        capTransaction.Rollback, UnitOfWork.Rollback) ?? exception;
                    if (!commitStarted && ReferenceEquals(failure, exception))
                        outcome = TransactionOutcome.RolledBack;
                }
            }
            finally
            {
                if (capTransaction != null)
                    failure = TransactionCleanup.DisposeAndCapture(capTransaction, failure);
                publisher.Transaction.Value = previousTransaction;
            }

            if (failure != null)
            {
                failure.SetTransactionOutcome(databaseCommitted ? TransactionOutcome.Committed : outcome);
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            return result;
        }
    }
}
