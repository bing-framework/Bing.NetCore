using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Exceptions
{
    /// <summary>
    /// 尝试
    /// </summary>
    public static class Try
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="createFunc">创建函数</param>
        public static Try<T> Create<T>(Func<T> createFunc)
        {
            try
            {
                return LiftValue(createFunc());
            }
            catch (Exception e)
            {
                return LiftException<T>(e);
            }
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="createFuncAsync">创建函数</param>
        public static Try<T> CreateFromTask<T>(Func<Task<T>> createFuncAsync)
        {
            try
            {
                return LiftValue(CallAsyncInSync(createFuncAsync));
            }
            catch (Exception e)
            {
                return LiftException<T>(e);
            }
        }

        /// <summary>
        /// 提取值
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="value">值</param>
        public static Try<T> LiftValue<T>(T value) => new Success<T>(value);

        /// <summary>
        /// 提取异常
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="e">异常</param>
        public static Try<T> LiftException<T>(Exception e) => new Failure<T>(e);

        /// <summary>
        /// 同步调异步
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="taskFunc">异步函数</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static TResult CallAsyncInSync<TResult>(Func<Task<TResult>> taskFunc, CancellationToken cancellationToken = default)
        {
            if (taskFunc is null)
                throw new ArgumentNullException(nameof(taskFunc));
            var task = Create(taskFunc).GetValue();
            if (task is null)
                throw new InvalidOperationException($"The task factory {nameof(taskFunc)} failed to run.");
            return ThenWaitAndUnwrapException(task, cancellationToken);
        }

        /// <summary>
        /// 等待执行并展开异常
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="task">异步</param>
        /// <param name="cancellationToken">取消令牌</param>
        private static TResult ThenWaitAndUnwrapException<TResult>(Task<TResult> task, CancellationToken cancellationToken)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));
            try
            {
                task.Wait(cancellationToken);
                return task.Result;
            }
            catch (AggregateException e)
            {
                throw ExceptionHelper.PrepareForRethrow(e.InnerException);
            }
        }
    }
}
