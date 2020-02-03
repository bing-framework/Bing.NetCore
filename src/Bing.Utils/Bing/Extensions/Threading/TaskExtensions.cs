using System;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 任务(<see cref="Task"/>) 扩展
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// 等待结果
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="task">异步操作</param>
        /// <param name="timeout">超时时间。单位：毫秒</param>
        /// <returns></returns>
        public static TResult WaitResult<TResult>(this Task<TResult> task, int timeout)
        {
            if (task.Wait(timeout))
            {
                return task.Result;
            }

            return default(TResult);
        }

        /// <summary>
        /// 设置Task过期时间
        /// </summary>
        /// <param name="task">异步操作</param>
        /// <param name="millisecondsDelay">超时时间。单位：毫秒</param>
        /// <returns></returns>
        public static async Task TimeoutAfter(this Task task, int millisecondsDelay)
        {
            var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask =
                await Task.WhenAny(task, Task.Delay(millisecondsDelay, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
            }
            else
            {
                throw new TimeoutException($"操作已超时。");
            }
        }

        /// <summary>
        /// 设置Task过期时间
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="task">异步操作</param>
        /// <param name="millisecondsDelay">超时时间。单位：毫秒</param>
        /// <returns></returns>
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, int millisecondsDelay)
        {
            var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask =
                await Task.WhenAny(task, Task.Delay(millisecondsDelay, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return task.Result;
            }
            else
            {
                throw new TimeoutException($"操作已超时。");
            }
        }
    }
}
