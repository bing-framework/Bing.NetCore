using System;
using System.Threading;

namespace Bing.Utils.Threading.Locks
{
    /// <summary>
    /// 读写锁释放器
    /// </summary>
    public class ReaderWriteLockDisposable : IDisposable
    {
        /// <summary>
        /// 读写锁
        /// </summary>
        private readonly ReaderWriterLockSlim _rwLock;

        /// <summary>
        /// 读写锁类型
        /// </summary>
        private readonly ReaderWriteLockType _readerWriteLockType;

        /// <summary>
        /// 初始化一个<see cref="ReaderWriteLockDisposable"/>类型的实例
        /// </summary>
        /// <param name="rwLock">读写锁</param>
        /// <param name="readerWriteLockType">读写锁类型</param>
        public ReaderWriteLockDisposable(ReaderWriterLockSlim rwLock,
            ReaderWriteLockType readerWriteLockType = ReaderWriteLockType.Write)
        {
            _rwLock = rwLock;
            _readerWriteLockType = readerWriteLockType;

            switch (_readerWriteLockType)
            {
                case ReaderWriteLockType.Read:
                    _rwLock.EnterReadLock();
                    break;
                case ReaderWriteLockType.Write:
                    _rwLock.EnterWriteLock();
                    break;
                case ReaderWriteLockType.UpgradeableRead:
                    _rwLock.EnterUpgradeableReadLock();
                    break;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        void IDisposable.Dispose()
        {
            switch (_readerWriteLockType)
            {
                case ReaderWriteLockType.Read:
                    _rwLock.ExitReadLock();
                    break;
                case ReaderWriteLockType.Write:
                    _rwLock.ExitWriteLock();
                    break;
                case ReaderWriteLockType.UpgradeableRead:
                    _rwLock.ExitUpgradeableReadLock();
                    break;
            }
        }
    }
}
