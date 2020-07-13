using System;
using System.Collections.Generic;

namespace Bing.Utils.Conversions.Internals
{
    /// <summary>
    /// 内部操作辅助类
    /// </summary>
    internal static class Helper
    {
        /// <summary>
        /// 是否某对象
        /// </summary>
        /// <typeparam name="TFrom">源类型</typeparam>
        /// <typeparam name="TTo">目标类型</typeparam>
        /// <param name="from">源对象</param>
        /// <param name="fromTry">源尝试</param>
        /// <param name="firstTry">首次尝试</param>
        /// <param name="tries">尝试方法</param>
        /// <param name="act">转换操作</param>
        // ReSharper disable once InconsistentNaming
        public static bool IsXXX<TFrom, TTo>(
            TFrom @from, 
            Func<TFrom, bool> fromTry,
            Func<TFrom, Action<TTo>, bool> firstTry, 
            IEnumerable<IConversionTry<TFrom, TTo>> tries,
            Action<TTo> act = null)
        {
            if (fromTry(from))
                return false;
            if (firstTry(from, act))
                return true;
            if (tries is null)
                return false;
            foreach (var @try in tries)
            {
                if (@try.Is(from, out var to))
                {
                    act?.Invoke(to);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 转换为某对象
        /// </summary>
        /// <typeparam name="TFrom">源类型</typeparam>
        /// <typeparam name="TTo">目标类型</typeparam>
        /// <param name="from">源对象</param>
        /// <param name="firstImpl">首次实现操作</param>
        /// <param name="impls">实现操作</param>
        // ReSharper disable once InconsistentNaming
        public static TTo ToXXX<TFrom, TTo>(
            TFrom @from,
            Func<TFrom, Action<TTo>, bool> firstImpl,
            IEnumerable<IConversionImpl<TFrom, TTo>> impls)
        {
            TTo result = default;
            if (firstImpl(from, to => result = to))
                return result;
            if (impls is null)
                return result;
            foreach (var impl in impls)
            {
                if (impl.TryTo(from, out result))
                    return result;
            }
            return result;
        }
    }
}
