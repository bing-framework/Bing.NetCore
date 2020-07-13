using System;
using System.Data;

namespace Bing.Datas.Transactions
{
    /// <summary>
    /// 事务包装器
    /// </summary>
    public interface ITransactionWrapper : IDisposable
    {
        /// <summary>
        /// 当前事务。如果没有事务，将返回null
        /// </summary>
        IDbTransaction CurrentTransaction { get; }

        /// <summary>
        /// 开始事务
        /// </summary>
        IDbTransaction Begin();

        /// <summary>
        /// 开始事务
        /// </summary>
        /// <param name="level">事务隔离级别</param>
        IDbTransaction Begin(IsolationLevel level);

        /// <summary>
        /// 提交
        /// </summary>
        /// <param name="clearTransaction">提交后清空事务</param>
        void Commit(bool clearTransaction = true);

        /// <summary>
        /// 回滚
        /// </summary>
        /// <param name="clearTransaction">回滚后清空事务</param>
        void Rollback(bool clearTransaction = true);

        /// <summary>
        /// 是否为空
        /// </summary>
        bool IsNull { get; }
    }
}
