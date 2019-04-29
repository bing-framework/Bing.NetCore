using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Caching
{
    /// <summary>
    /// 异步锁
    /// </summary>
    internal class AsyncLock
    {
        /// <summary>
        /// 信号量修正
        /// </summary>
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        /// <summary>
        /// 释放器
        /// </summary>
        private readonly Releaser _releaser;

        /// <summary>
        /// 异步释放器
        /// </summary>
        private readonly Task<Releaser> _releaserTask;

        /// <summary>
        /// 初始化一个<see cref="AsyncLock"/>类型的实例
        /// </summary>
        public AsyncLock()
        {
            _releaser = new Releaser(this);
            _releaserTask = Task.FromResult(_releaser);
        }

        /// <summary>
        /// 锁定
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public Task<Releaser> LockAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var wait = _semaphore.WaitAsync(cancellationToken);
            return wait.IsCompleted
                ? _releaserTask
                : wait.ContinueWith((_, state) => ((AsyncLock)state)._releaser, this, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        /// <summary>
        /// 锁定
        /// </summary>
        /// <returns></returns>
        public Releaser Lock()
        {
            _semaphore.Wait();
            return _releaser;
        }

        /// <summary>
        /// 释放器
        /// </summary>
        public struct Releaser : IDisposable
        {
            /// <summary>
            /// 即将释放资源的异步锁
            /// </summary>
            private readonly AsyncLock _toRelease;

            /// <summary>
            /// 初始化一个<see cref="Releaser"/>类型的实例
            /// </summary>
            /// <param name="toRelease">异步锁</param>
            internal Releaser(AsyncLock toRelease)
            {
                _toRelease = toRelease;
            }

            /// <summary>
            /// 释放资源
            /// </summary>
            public void Dispose() => _toRelease?._semaphore?.Dispose();
        }
    }
}
