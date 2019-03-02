using System;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 任务工厂(<see cref="TaskFactory"/>) 扩展
    /// </summary>
    public static class TaskFactoryExtensions
    {
        /// <summary>
        /// 启动延时任务
        /// </summary>
        /// <param name="factory">任务工厂</param>
        /// <param name="millisecondsDelay">延时时间。单位：毫秒</param>
        /// <param name="action">操作</param>
        /// <returns></returns>
        public static Task StartDelayedTask(this TaskFactory factory, int millisecondsDelay, Action action)
        {
            // 校验参数
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (millisecondsDelay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            // 检查预先取消令牌
            if (factory.CancellationToken.IsCancellationRequested)
            {
                return new Task(() => { }, factory.CancellationToken);
            }

            // 创建定时任务
            var tcs = new TaskCompletionSource<object>(factory.CreationOptions);
            var ctr = default(CancellationTokenRegistration);

            // 创建计时器但尚未启动它。如果我们现在开始，它可能会在ctr设置为正确注册之前出发。
            var ctr1 = ctr;
            var timer = new Timer(self =>
            {
                // 清除取消令牌和计时器，并尝试转换为已完成状态
                ctr1.Dispose();
                ((Timer) self).Dispose();
                tcs.TrySetResult(null);
            }, null, -1, -1);

            // 注册取消令牌
            if (factory.CancellationToken.CanBeCanceled)
            {
                // 取消时，释放计时器并尝试转换为已取消状态。
                factory.CancellationToken.Register(() =>
                {
                    timer.Dispose();
                    tcs.TrySetCanceled();
                });
            }

            // 启动计时器并提交任务
            try
            {
                timer.Change(millisecondsDelay, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
            }

            return tcs.Task.ContinueWith(_ => action(), factory.CancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion, factory.Scheduler ?? TaskScheduler.Current);
        }
    }
}
