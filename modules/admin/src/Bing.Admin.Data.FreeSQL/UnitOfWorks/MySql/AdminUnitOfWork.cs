using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Transaction;
using Bing.FreeSQL;
using DotNetCore.CAP;

namespace Bing.Admin.Data.UnitOfWorks.MySql
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public class AdminUnitOfWork : Bing.Uow.UnitOfWork, IAdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="AdminUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="orm">FreeSql</param>
        /// <param name="serviceProvider">服务提供器</param>
        public AdminUnitOfWork(FreeSqlWrapper orm, IServiceProvider serviceProvider = null) : base(orm, serviceProvider)
        {
        }

        /// <summary>
        /// 异步保存更改
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            var transactionActionManager = LazyServiceProvider.LazyGetService<ITransactionActionManager>();
            if (transactionActionManager.Count == 0)
                return await base.SaveChangesAsync(cancellationToken);
            return await TransactionCommit(transactionActionManager, cancellationToken);
        }

        /// <summary>
        /// 手工创建事务提交
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager, CancellationToken cancellationToken)
        {
            var publisher = LazyServiceProvider.LazyGetService<ICapPublisher>();
            var capTransaction = UnitOfWork.BeginTransaction(publisher, autoCommit: false);
            await transactionActionManager.CommitAsync(
                publisher.Transaction.Value.DbTransaction as IDbTransaction);
            var result = await base.SaveChangesAsync(cancellationToken);
            UnitOfWork.Commit(capTransaction);
            return result;
            //try
            //{
            //    await transactionActionManager.CommitAsync(
            //        publisher.Transaction.Value.DbTransaction as IDbTransaction);
            //    var result = await base.SaveChangesAsync(cancellationToken);
            //    await capTransaction.CommitAsync(cancellationToken);
            //    return result;
            //}
            //catch (Exception e)
            //{
            //    await capTransaction.RollbackAsync(cancellationToken);
            //    throw;
            //}
        }
    }
}
