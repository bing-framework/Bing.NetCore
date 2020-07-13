using System;
using System.Collections.Generic;

namespace Bing.Exceptions
{
    /// <summary>
    /// 尝试成功
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public class Success<T> : Try<T>
    {
        /// <summary>
        /// 是否失败
        /// </summary>
        public override bool IsFailure => false;

        /// <summary>
        /// 是否成功
        /// </summary>
        public override bool IsSuccess => true;

        /// <summary>
        /// 异常
        /// </summary>
        public override Exception Exception => null;

        /// <summary>
        /// 值
        /// </summary>
        public override T Value { get; }

        /// <summary>
        /// 初始化一个<see cref="Success{T}"/>类型的实例
        /// </summary>
        /// <param name="value">值</param>
        internal Success(T value) => Value = value;

        /// <summary>
        /// 重载输出字符串
        /// </summary>
        public override string ToString() => $"Success<{Value}>";

        /// <summary>
        /// 相等
        /// </summary>
        /// <param name="other">对象</param>
        public bool Equals(Success<T> other) => !(other is null) && EqualityComparer<T>.Default.Equals(Value, other.Value);

        /// <summary>
        /// 重载相等
        /// </summary>
        /// <param name="obj">对象</param>
        public override bool Equals(object obj) => obj is Success<T> success && Equals(success);

        /// <summary>
        /// 重载获取哈希码
        /// </summary>
        public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Value);

        /// <summary>
        /// 解构
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="exception">异常</param>
        public override void Deconstruct(out T value, out Exception exception)
        {
            value = Value;
            exception = default;
        }

        /// <summary>
        /// 恢复
        /// </summary>
        /// <param name="recoverFunc">恢复函数</param>
        public override Try<T> Recover(Func<Exception, T> recoverFunc) => this;

        /// <summary>
        /// 恢复
        /// </summary>
        /// <param name="recoverFunc">恢复函数</param>
        public override Try<T> RecoverWith(Func<Exception, Try<T>> recoverFunc) => this;

        /// <summary>
        /// 匹配
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="whenValue">条件值函数</param>
        /// <param name="whenException">条件异常</param>
        public override TResult Match<TResult>(Func<T, TResult> whenValue, Func<Exception, TResult> whenException)
        {
            if (whenValue is null)
                throw new ArgumentNullException(nameof(whenValue));
            return whenValue(Value);
        }

        /// <summary>
        /// 触发
        /// </summary>
        /// <param name="successAction">成功操作</param>
        /// <param name="failureAction">失败操作</param>
        public override Try<T> Tap(Action<T> successAction = null, Action<Exception> failureAction = null)
        {
            successAction?.Invoke(Value);
            return this;
        }

        /// <summary>
        /// 绑定
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="bind">绑定函数</param>
        internal override Try<TResult> Bind<TResult>(Func<T, Try<TResult>> bind)
        {
            if (bind is null)
                throw new ArgumentNullException(nameof(bind));
            return bind(Value);
        }
    }
}
