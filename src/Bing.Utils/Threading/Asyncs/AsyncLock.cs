using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Utils.Threading.Asyncs
{
    /// <summary>
    /// 异步锁
    /// </summary>
    public class AsyncLock
    {
        /// <summary>
        /// 资源释放器
        /// </summary>
        private readonly Task<Releaser> _releaser;

        /// <summary>
        /// 异步信号量
        /// </summary>
        private readonly AsyncSemaphore _semaphore;

        /// <summary>
        /// 初始一个<see cref="AsyncLock"/>类型的实例
        /// </summary>
        public AsyncLock()
        {
            _semaphore = new AsyncSemaphore(1);
            _releaser = Task.FromResult(new Releaser(this));
        }

        /// <summary>
        /// 异步锁定
        /// </summary>
        public Task<Releaser> LockAsync()
        {
            var wait = _semaphore.WaitAsync();
            return wait.IsCompleted
                ? _releaser
                : wait.ContinueWith((_, state) => new Releaser((AsyncLock)state)
                    , this
                    , CancellationToken.None
                    , TaskContinuationOptions.ExecuteSynchronously
                    , TaskScheduler.Default);
        }

        /// <summary>
        /// 资源释放器
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
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            /// <filterpriority>2</filterpriority>
            public void Dispose()
            {
                _toRelease?._semaphore.Release();
            }
        }
    }
}
