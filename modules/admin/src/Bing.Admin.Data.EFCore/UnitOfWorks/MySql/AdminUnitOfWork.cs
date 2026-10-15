using System;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Transaction;
using Bing.Admin.Data;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;

namespace Bing.Admin.Data.UnitOfWorks.MySql
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public class AdminUnitOfWork : Bing.Datas.EntityFramework.MySql.MySqlUnitOfWorkBase, IAdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="AdminUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="options">配置项</param>
        public AdminUnitOfWork( DbContextOptions<AdminUnitOfWork> options, IServiceProvider serviceProvider) : base( options , serviceProvider)
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
        /// <returns></returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        protected override int TransactionCommit(ITransactionActionManager transactionActionManager) =>
            TransactionCommitAsync(transactionActionManager, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// 手工创建事务提交
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="cancellationToken">取消令牌</param>
        protected override async Task<int> TransactionCommitAsync(ITransactionActionManager transactionActionManager,
            CancellationToken cancellationToken)
        {
            var publisher = LazyServiceProvider.LazyGetService<ICapPublisher>();
            var previousTransaction = publisher.Transaction.Value;
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction capTransaction = null;
            var databaseCommitted = false;
            var commitStarted = false;
            var outcome = TransactionOutcome.Unknown;
            Exception failure = null;
            var result = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                capTransaction = Database.BeginTransaction(publisher, autoCommit: false);
                await transactionActionManager.CommitAsync(
                    publisher.Transaction.Value.DbTransaction as IDbTransaction);
                cancellationToken.ThrowIfCancellationRequested();
                result = await SaveChangesCoreAsync(cancellationToken);
                commitStarted = true;
                await capTransaction.CommitAsync(cancellationToken);
                databaseCommitted = true;
            }
            catch (Exception exception)
            {
                failure = capTransaction == null
                    ? exception
                    : await TryRollbackAsync(exception, capTransaction.RollbackAsync) ?? exception;
                if (capTransaction != null && !commitStarted && ReferenceEquals(failure, exception))
                    outcome = TransactionOutcome.RolledBack;
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

        /// <summary>
        /// 使用独立令牌清理事务并保留原始异常。
        /// </summary>
        internal static async Task<AggregateException> TryRollbackAsync(Exception original,
            Func<CancellationToken, Task> rollback)
        {
            try
            {
                await rollback(CancellationToken.None);
                return null;
            }
            catch (Exception rollbackException)
            {
                return new AggregateException(original, rollbackException);
            }
        }
    }
}
