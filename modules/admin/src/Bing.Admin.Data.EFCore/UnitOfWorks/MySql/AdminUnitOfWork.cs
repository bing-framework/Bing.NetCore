﻿using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Transaction;
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
        /// 初始化一个<see cref="AdminUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置项</param>
        public AdminUnitOfWork( DbContextOptions<AdminUnitOfWork> options, IServiceProvider serviceProvider) : base( options , serviceProvider)
        {
        }

        /// <summary>异步保存更改</summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            SaveChangesBefore();
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
        private async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager,
            CancellationToken cancellationToken)
        {
            using (var connection = Database.GetDbConnection())
            {
                if (connection.State == ConnectionState.Closed)
                    await connection.OpenAsync(cancellationToken);
                var publisher = LazyServiceProvider.LazyGetService<ICapPublisher>();
                using (var capTransaction = Database.BeginTransaction(publisher, autoCommit: false))
                {
                    try
                    {
                        await transactionActionManager.CommitAsync(
                            publisher.Transaction.Value.DbTransaction as IDbTransaction);
                        var result = await base.SaveChangesAsync(cancellationToken);
                        await capTransaction.CommitAsync(cancellationToken);
                        return result;
                    }
                    catch (Exception)
                    {
                        await capTransaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                }
            }
        }
    }
}
