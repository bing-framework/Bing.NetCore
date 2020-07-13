using System;

// ReSharper disable once CheckNamespace
namespace Bing.Exceptions
{
    /// <summary>
    /// 尝试(<see cref="Try{T}"/>) 扩展
    /// </summary>
    public static class TryExtensions
    {
        /// <summary>
        /// 选择
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="selector">选择器</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static Try<TResult> Select<TSource, TResult>(this Try<TSource> source, Func<TSource, TResult> selector)
        {
            if(source is null)
                throw new ArgumentNullException(nameof(source));
            if(selector is null)
                throw new ArgumentNullException(nameof(selector));
            return source.Bind(val => Try.LiftValue(selector(val)));
        }

        /// <summary>
        /// 选择
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="selector">选择器</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static Try<TResult> SelectMany<TSource, TResult>(this Try<TSource> source, Func<TSource, Try<TResult>> selector)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (selector is null)
                throw new ArgumentNullException(nameof(selector));
            return source.Bind(selector);
        }

        /// <summary>
        /// 选择
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <typeparam name="TIntermediate">中间类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="convert">转换器</param>
        /// <param name="selector">选择器</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static Try<TResult> SelectMany<TSource, TIntermediate, TResult>(this Try<TSource> source, Func<TSource, Try<TIntermediate>> convert, Func<TSource, TIntermediate, TResult> selector)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if(convert is null)
                throw new ArgumentNullException(nameof(convert));
            if (selector is null)
                throw new ArgumentNullException(nameof(selector));
            return source.Bind(val => convert(val).Select(interVal => selector(val, interVal)));
        }

        /// <summary>
        /// 设置条件
        /// </summary>
        /// <typeparam name="TSource">源类型</typeparam>
        /// <param name="source">源对象</param>
        /// <param name="predicate">条件</param>
        public static Try<TSource> Where<TSource>(this Try<TSource> source, Func<TSource, bool> predicate)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));
            return source.Bind(val =>
                predicate(val)
                    ? source
                    : Try.LiftException<TSource>(new InvalidOperationException("Predicate not satisfied")));
        }
    }
}
