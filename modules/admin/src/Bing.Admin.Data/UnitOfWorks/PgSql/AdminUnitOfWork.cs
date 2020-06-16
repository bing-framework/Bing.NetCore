using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Bing.Datas.Transactions;
using Bing.Helpers;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;

namespace Bing.Admin.Data.UnitOfWorks.PgSql
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public class AdminUnitOfWork : Bing.Datas.EntityFramework.PgSql.UnitOfWork, IAdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="AdminUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置项</param>
        public AdminUnitOfWork(DbContextOptions<AdminUnitOfWork> options) : base(options)
        {
        }

        /// <summary>
        /// 异步保存更改
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesBefore();
            var transactionActionManager = Ioc.Create<ITransactionActionManager>();
            if (transactionActionManager.Count == 0)
                return await base.SaveChangesAsync(cancellationToken);
            return await TransactionCommit(transactionActionManager, cancellationToken);
        }

        /// <summary>
        /// 手工创建事务提交
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager,
            CancellationToken cancellationToken)
        {
            using (var connection = Database.GetDbConnection())
            {
                if (connection.State == ConnectionState.Closed)
                    await connection.OpenAsync(cancellationToken);
                var publisher = Ioc.Create<ICapPublisher>();
                using (var capTransaction = Database.BeginTransaction(publisher, autoCommit: false))
                {
                    try
                    {
                        await transactionActionManager.CommitAsync(
                            publisher.Transaction.Value.DbTransaction as IDbTransaction);
                        var result = await base.SaveChangesAsync(cancellationToken);
                        capTransaction.Commit();
                        return result;
                    }
                    catch (Exception)
                    {
                        capTransaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
