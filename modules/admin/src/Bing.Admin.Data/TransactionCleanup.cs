using System;

namespace Bing.Admin.Data
{
    /// <summary>
    /// 汇总事务释放期间发生的异常。
    /// </summary>
    internal static class TransactionCleanup
    {
        /// <summary>
        /// 释放事务并保留此前发生的异常。
        /// </summary>
        internal static Exception DisposeAndCapture(IDisposable transaction, Exception failure)
        {
            try
            {
                transaction.Dispose();
            }
            catch (Exception cleanupException)
            {
                return failure == null
                    ? cleanupException
                    : new AggregateException(failure, cleanupException);
            }

            return failure;
        }
    }
}
