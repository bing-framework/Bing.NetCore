using System;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 锁定扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 锁定给定对象标识，执行委托
        /// </summary>
        /// <param name="source">对象标识</param>
        /// <param name="action">操作</param>
        public static void Locking(this object source, Action action)
        {
            lock (source)
                action();
        }

        /// <summary>
        /// 锁定给定源数据，执行委托
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">源数据</param>
        /// <param name="action">操作</param>
        public static void Locking<T>(this T source, Action<T> action)
        {
            lock (source)
                action(source);
        }

        /// <summary>
        /// 锁定给定对象标识，执行委托
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="source">对象标识</param>
        /// <param name="func">操作</param>
        public static TResult Locking<TResult>(this object source, Func<TResult> func)
        {
            lock (source)
                return func();
        }

        /// <summary>
        /// 锁定给定源数据，执行委托
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="source">源数据</param>
        /// <param name="func">操作</param>
        public static TResult Locking<TSource, TResult>(this TSource source, Func<TSource, TResult> func)
        {
            lock (source)
                return func(source);
        }
    }
}
