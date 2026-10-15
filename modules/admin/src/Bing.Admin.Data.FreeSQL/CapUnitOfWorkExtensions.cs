using System.Reflection;
using System;
using System.Collections.Generic;
using DotNetCore.CAP;
using DotNetCore.CAP.Transport;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Data
{
    /// <summary>
    /// 为 FreeSQL 工作单元接入 CAP 事务。
    /// </summary>
    internal static class CapUnitOfWorkExtensions
    {
        /// <summary>
        /// 在业务事务上启动 CAP 事务。
        /// </summary>
        public static ICapTransaction BeginTransaction(this IUnitOfWork unitOfWork, ICapPublisher publisher, bool autoCommit = false)
            => BeginTransaction(unitOfWork, publisher, dispatcher => new MySqlCapTransaction(dispatcher), autoCommit);

        /// <summary>
        /// 在业务事务上启动 CAP 事务。
        /// </summary>
        internal static ICapTransaction BeginTransaction(IUnitOfWork unitOfWork, ICapPublisher publisher,
            Func<IDispatcher, ICapTransaction> createTransaction, bool autoCommit = false)
        {
            var capTransaction = createTransaction(publisher.ServiceProvider.GetRequiredService<IDispatcher>());
            var previousTransaction = publisher.Transaction.Value;
            var createdDatabaseTransaction = false;
            try
            {
                createdDatabaseTransaction = unitOfWork.GetOrBeginTransaction(false) == null;
                var databaseTransaction = unitOfWork.GetOrBeginTransaction();
                var startedTransaction = capTransaction.Begin(databaseTransaction, autoCommit);
                publisher.Transaction.Value = startedTransaction;
                return startedTransaction;
            }
            catch (Exception exception)
            {
                publisher.Transaction.Value = previousTransaction;
                var errors = new List<Exception> { exception };
                if (createdDatabaseTransaction)
                {
                    try
                    {
                        unitOfWork.Rollback();
                    }
                    catch (Exception cleanupException)
                    {
                        errors.Add(cleanupException);
                    }
                }
                try
                {
                    capTransaction.Dispose();
                }
                catch (Exception cleanupException)
                {
                    errors.Add(cleanupException);
                }

                if (errors.Count > 1)
                    throw new AggregateException(errors);
                throw;
            }
        }

        /// <summary>
        /// 刷新 CAP 事务中暂存的消息。
        /// </summary>
        /// <param name="capTransaction">待刷新的 CAP 事务。</param>
        public static void Flush(this ICapTransaction capTransaction)
        {
            if (capTransaction == null)
                throw new ArgumentNullException(nameof(capTransaction));
            var flush = capTransaction.GetType().GetMethod("Flush", BindingFlags.Instance | BindingFlags.NonPublic);
            if (flush == null)
                throw new MissingMethodException(capTransaction.GetType().FullName, "Flush");
            try
            {
                flush.Invoke(capTransaction, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        /// <summary>
        /// 提交业务事务并刷新 CAP 消息。
        /// </summary>
        public static void Commit(this IUnitOfWork unitOfWork, ICapTransaction capTransaction,
            Action onDatabaseCommitted = null)
        {
            unitOfWork.Commit();
            onDatabaseCommitted?.Invoke();
            capTransaction.Flush();
        }

        /// <summary>
        /// 分别回滚 CAP 与业务事务，并保留原始异常。
        /// </summary>
        internal static AggregateException RollbackBoth(Exception original, Action rollbackCap,
            Action rollbackDatabase)
        {
            var errors = new List<Exception> { original };
            try
            {
                rollbackCap();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                rollbackDatabase();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            return errors.Count == 1 ? null : new AggregateException(errors);
        }
    }
}
