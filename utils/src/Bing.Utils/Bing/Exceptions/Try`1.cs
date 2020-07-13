using System;
using System.Threading.Tasks;

namespace Bing.Exceptions
{
    /// <summary>
    /// 尝试
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public abstract class Try<T>
    {
        /// <summary>
        /// 是否失败
        /// </summary>
        public abstract bool IsFailure { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public abstract bool IsSuccess { get; }

        /// <summary>
        /// 异常
        /// </summary>
        public abstract Exception Exception { get; }

        /// <summary>
        /// 值
        /// </summary>
        public abstract T Value { get; }

        /// <summary>
        /// 获取值
        /// </summary>
        public virtual T GetValue() => Value;

        /// <summary>
        /// 获取值
        /// </summary>
        public virtual Task<T> GetValueAsync() => IsSuccess ? Task.FromResult(Value) : FromException<T>(Exception);

        /// <summary>
        /// 安全获取值
        /// </summary>
        public virtual T GetSafeValue() => IsSuccess ? Value : default;

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultVal">默认值</param>
        public virtual T GetSafeValue(T defaultVal) => IsSuccess ? Value : defaultVal;

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultValFunc">默认值函数</param>
        public virtual T GetSafeValue(Func<T> defaultValFunc) => IsSuccess ? Value : defaultValFunc is null ? default : defaultValFunc();

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultValFunc">默认值函数</param>
        public virtual T GetSafeValue(Func<Exception, T> defaultValFunc) => IsSuccess ? Value : defaultValFunc is null ? default : defaultValFunc(Exception);

        /// <summary>
        /// 安全获取值
        /// </summary>
        public virtual Task<T> GetSafeValueAsync() => Task.FromResult(GetSafeValue());

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultVal">默认值</param>
        public virtual Task<T> GetSafeValueAsync(T defaultVal) => Task.FromResult(GetSafeValue(defaultVal));

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultValFunc">默认值函数</param>
        public virtual Task<T> GetSafeValueAsync(Func<T> defaultValFunc) => Task.FromResult(GetSafeValue(defaultValFunc));

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultValFunc">默认值函数</param>
        public virtual Task<T> GetSafeValueAsync(Func<Exception, T> defaultValFunc) => Task.FromResult(GetSafeValue(defaultValFunc));

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultValAsyncFunc">默认值函数</param>
        public virtual Task<T> GetSafeValueAsync(Func<Task<T>> defaultValAsyncFunc) => IsSuccess ? Task.FromResult(Value) : defaultValAsyncFunc is null ? Task.FromResult(default(T)) : defaultValAsyncFunc();

        /// <summary>
        /// 安全获取值
        /// </summary>
        /// <param name="defaultValAsyncFunc">默认值函数</param>
        public virtual Task<T> GetSafeValueAsync(Func<Exception, Task<T>> defaultValAsyncFunc) => IsSuccess ? Task.FromResult(Value) : defaultValAsyncFunc is null ? Task.FromResult(default(T)) : defaultValAsyncFunc(Exception);

        /// <summary>
        /// 尝试获取值
        /// </summary>
        /// <param name="value">值</param>
        public virtual bool TryGetValue(out T value)
        {
            value = GetSafeValue();
            return IsSuccess;
        }

        /// <summary>
        /// 尝试获取值
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="defaultVal">默认值</param>
        public virtual bool TryGetValue(out T value, T defaultVal)
        {
            value = GetSafeValue(defaultVal);
            return true;
        }

        /// <summary>
        /// 尝试获取值
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="defaultValFunc">默认值函数</param>
        public virtual bool TryGetValue(out T value, Func<T> defaultValFunc)
        {
            try
            {
                value = GetSafeValue(defaultValFunc);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// 尝试获取值
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="defaultValFunc">默认值函数</param>
        public virtual bool TryGetValue(out T value, Func<Exception, T> defaultValFunc)
        {
            try
            {
                value = GetSafeValue(defaultValFunc);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// ==
        /// </summary>
        /// <param name="left">左对象</param>
        /// <param name="right">右对象</param>
        public static bool operator ==(Try<T> left, Try<T> right)
        {
            if (left is null && right is null)
                return true;
            if (left is null || right is null)
                return false;
            return left.Equals(right);
        }

        /// <summary>
        /// !=
        /// </summary>
        /// <param name="left">左对象</param>
        /// <param name="right">右对象</param>
        public static bool operator !=(Try<T> left, Try<T> right) => !(left == right);

        /// <summary>
        /// 重载输出字符串
        /// </summary>
        public abstract override string ToString();

        /// <summary>
        /// 重载相等
        /// </summary>
        /// <param name="obj">对象</param>
        public abstract override bool Equals(object obj);

        /// <summary>
        /// 重载获取哈希码
        /// </summary>
        public abstract override int GetHashCode();

        /// <summary>
        /// 解构
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="exception">异常</param>
        public abstract void Deconstruct(out T value, out Exception exception);

        /// <summary>
        /// 将异常转换为指定类型异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        public TException ExceptionAs<TException>() where TException : Exception => Exception as TException;

        /// <summary>
        /// 恢复
        /// </summary>
        /// <param name="recoverFunc">恢复函数</param>
        public abstract Try<T> Recover(Func<Exception, T> recoverFunc);

        /// <summary>
        /// 恢复
        /// </summary>
        /// <param name="recoverFunc">恢复函数</param>
        public abstract Try<T> RecoverWith(Func<Exception, Try<T>> recoverFunc);

        /// <summary>
        /// 匹配
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="whenValue">条件值函数</param>
        /// <param name="whenException">条件异常</param>
        public abstract TResult Match<TResult>(Func<T, TResult> whenValue, Func<Exception, TResult> whenException);

        /// <summary>
        /// 映射
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="map">映射函数</param>
        public Try<TResult> Map<TResult>(Func<T, TResult> map)
        {
            if (map is null)
                throw new ArgumentNullException(nameof(map));
            return Bind(value => Try.Create(() => map(value)));
        }

        /// <summary>
        /// 触发
        /// </summary>
        /// <param name="successAction">成功操作</param>
        /// <param name="failureAction">失败操作</param>
        public abstract Try<T> Tap(Action<T> successAction = null, Action<Exception> failureAction = null);

        /// <summary>
        /// 绑定
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="bind">绑定函数</param>
        internal abstract Try<TResult> Bind<TResult>(Func<T, Try<TResult>> bind);

        /// <summary>
        /// 将异常转换为指定类型结果
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="exception">异常</param>
        private static Task<TResult> FromException<TResult>(Exception exception) => Task.FromException<TResult>(exception);
    }
}
