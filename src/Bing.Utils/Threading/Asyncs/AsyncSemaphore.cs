using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Utils.Extensions;

namespace Bing.Utils.Threading.Asyncs
{
    /// <summary>
    /// 异步信号量
    /// </summary>
    public class AsyncSemaphore
    {
        /// <summary>
        /// 是否完成操作
        /// </summary>
        private static readonly Task Completed = Task.FromResult(true);

        /// <summary>
        /// 等待队列
        /// </summary>
        private readonly Queue<TaskCompletionSource<bool>> _waiters = new Queue<TaskCompletionSource<bool>>();

        /// <summary>
        /// 当前计数器
        /// </summary>
        private int _currentCount;

        /// <summary>
        /// 初始化一个<see cref="AsyncSemaphore"/>类型的实例
        /// </summary>
        /// <param name="initialCount">初始化计数器</param>
        public AsyncSemaphore(int initialCount)
        {
            initialCount.CheckGreaterThan(nameof(initialCount), 0);
            _currentCount = initialCount;
        }

        /// <summary>
        /// 等待同步
        /// </summary>
        /// <returns></returns>
        public Task WaitAsync()
        {
            lock (_waiters)
            {
                if (_currentCount > 0)
                {
                    --_currentCount;
                    return Completed;
                }

                var waiter = new TaskCompletionSource<bool>();
                _waiters.Enqueue(waiter);
                return waiter.Task;
            }
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Release()
        {
            TaskCompletionSource<bool> toRelease = null;
            lock (_waiters)
            {
                if (_waiters.Count > 0)
                {
                    toRelease = _waiters.Dequeue();
                }
                else
                {
                    ++_currentCount;
                }
            }

            toRelease?.SetResult(true);
        }
    }
}
