using System;
using System.Runtime.ExceptionServices;

namespace Bing.Exceptions
{
    /// <summary>
    /// 尝试失败
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public class Failure<T> : Try<T>
    {
        /// <summary>
        /// 是否失败
        /// </summary>
        public override bool IsFailure => true;

        /// <summary>
        /// 是否成功
        /// </summary>
        public override bool IsSuccess => false;

        /// <summary>
        /// 异常
        /// </summary>
        public override Exception Exception { get; }

        /// <summary>
        /// 值
        /// </summary>
        public override T Value => throw Rethrow();

        /// <summary>
        /// 初始化一个<see cref="Failure{T}"/>类型的实例
        /// </summary>
        /// <param name="exception">异常</param>
        internal Failure(Exception exception) => Exception = exception ?? new ArgumentNullException(nameof(exception));

        /// <summary>
        /// 重载输出字符串
        /// </summary>
        public override string ToString() => $"Failure<{Exception}>";

        /// <summary>
        /// 相等
        /// </summary>
        /// <param name="other">对象</param>
        public bool Equals(Failure<T> other) => !(other is null) && ReferenceEquals(Exception, other.Exception);

        /// <summary>
        /// 重载相等
        /// </summary>
        /// <param name="obj">对象</param>
        public override bool Equals(object obj) => obj is Failure<T> failure && Equals(failure);

        /// <summary>
        /// 重载获取哈希码
        /// </summary>
        public override int GetHashCode() => Exception.GetHashCode();

        /// <summary>
        /// 解构
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="exception">异常</param>
        public override void Deconstruct(out T value, out Exception exception)
        {
            value = default;
            exception = Exception;
        }

        /// <summary>
        /// 恢复
        /// </summary>
        /// <param name="recoverFunc">恢复函数</param>
        public override Try<T> Recover(Func<Exception, T> recoverFunc) => RecoverWith(ex => Try.LiftValue(recoverFunc(ex)));

        /// <summary>
        /// 恢复
        /// </summary>
        /// <param name="recoverFunc">恢复函数</param>
        public override Try<T> RecoverWith(Func<Exception, Try<T>> recoverFunc)
        {
            try
            {
                return recoverFunc(Exception);
            }
            catch (Exception e)
            {
                return new Failure<T>(e);
            }
        }

        /// <summary>
        /// 匹配
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="whenValue">条件值函数</param>
        /// <param name="whenException">条件异常</param>
        public override TResult Match<TResult>(Func<T, TResult> whenValue, Func<Exception, TResult> whenException)
        {
            if (whenException is null)
                throw new ArgumentNullException(nameof(whenException));
            return whenException(Exception);
        }

        /// <summary>
        /// 触发
        /// </summary>
        /// <param name="successAction">成功操作</param>
        /// <param name="failureAction">失败操作</param>
        public override Try<T> Tap(Action<T> successAction = null, Action<Exception> failureAction = null)
        {
            failureAction?.Invoke(Exception);
            return this;
        }

        /// <summary>
        /// 绑定
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="bind">绑定函数</param>
        internal override Try<TResult> Bind<TResult>(Func<T, Try<TResult>> bind) => Try.LiftException<TResult>(Exception);

        /// <summary>
        /// 重抛异常
        /// </summary>
        private Exception Rethrow() => ExceptionDispatchInfo.Capture(Exception).SourceException;
    }
}
