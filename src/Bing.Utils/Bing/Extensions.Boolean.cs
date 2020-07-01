using System;
using Bing.Exceptions;

namespace Bing
{
    /// <summary>
    /// 布尔值(<see cref="bool"/>) 扩展
    /// </summary>
    public static class BooleanExtensions
    {
        #region IfTrue(如果为true，则执行方法)

        /// <summary>
        /// 如果为true，则执行方法
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="action">执行方法</param>
        public static void IfTrue(this bool @this, Action action)
        {
            if (@this)
                action?.Invoke();
        }

        /// <summary>
        /// 如果为true，则执行方法
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="action">执行方法</param>
        public static void IfTrue(this bool? @this, Action action)
        {
            if (@this.GetValueOrDefault())
                action?.Invoke();
        }

        #endregion

        #region IfFalse(如果为false，则执行方法)

        /// <summary>
        /// 如果为false，则执行方法
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="action">执行方法</param>
        public static void IfFalse(this bool @this, Action action)
        {
            if (!@this)
                action?.Invoke();
        }

        /// <summary>
        /// 如果为false，则执行方法
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="action">执行方法</param>
        public static void IfFalse(this bool? @this, Action action)
        {
            if (!@this.GetValueOrDefault())
                action?.Invoke();
        }

        #endregion

        #region IfTrue(如果为true，则输出参数)

        /// <summary>
        /// 如果为true，则输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">值</param>
        /// <param name="t">输出参数</param>
        public static T IfTrue<T>(this bool @this, T t) => @this ? t : default;

        /// <summary>
        /// 如果为true，则输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">值</param>
        /// <param name="t">输出参数</param>
        public static T IfTrue<T>(this bool? @this, T t) => @this.GetValueOrDefault() ? t : default;

        #endregion

        #region IfFalse(如果为false时，则输出参数)

        /// <summary>
        /// 如果为false时，则输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">值</param>
        /// <param name="t">输出参数</param>
        public static T IfFalse<T>(this bool @this, T t) => !@this ? t : default;

        /// <summary>
        /// 如果为false时，则输出参数
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">值</param>
        /// <param name="t">输出参数</param>
        public static T IfFalse<T>(this bool? @this, T t) => !@this.GetValueOrDefault() ? t : default;

        #endregion

        #region IfTtt(如果条件成立则A，不成立则B)

        /// <summary>
        /// 如果条件成立则A，不成立则B
        /// </summary>
        /// <param name="this">条件</param>
        /// <param name="trueAction">执行方法</param>
        /// <param name="falseAction">执行方法</param>
        public static void IfTtt(this bool @this, Action trueAction, Action falseAction)
        {
            if (@this)
                trueAction?.Invoke();
            else
                falseAction?.Invoke();
        }

        /// <summary>
        /// 如果条件成立则A，不成立则B
        /// </summary>
        /// <param name="this">条件</param>
        /// <param name="trueFunc">执行返回函数</param>
        /// <param name="falseFunc">执行返回函数</param>
        public static T IfTtt<T>(this bool @this, Func<T> trueFunc, Func<T> falseFunc)
        {
            if (@this)
                return trueFunc == null ? default : trueFunc.Invoke();
            else
                return falseFunc == null ? default : falseFunc.Invoke();
        }

        #endregion

        #region IfTrueThenThrow(如果为true，则抛异常)

        /// <summary>
        /// 如果为true，则抛异常
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="exception">异常</param>
        public static void IfTrueThenThrow(this bool @this, Exception exception) =>
            @this.IfTrue(() => ExceptionHelper.PrepareForRethrow(exception));

        #endregion

        #region IfFalseThenThrow(如果为false，则抛异常)

        /// <summary>
        /// 如果为false，则抛异常
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="exception">异常</param>
        public static void IfFalseThenThrow(this bool @this, Exception exception) =>
            @this.IfFalse(() => ExceptionHelper.PrepareForRethrow(exception));

        #endregion

        #region ToBinary(转换为字节)

        /// <summary>
        /// 转换为字节
        /// </summary>
        /// <param name="this">值</param>
        public static byte ToBinary(this bool @this) => Convert.ToByte(@this);

        #endregion

        #region ToString(转换为字符串)

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="trueString">字符串</param>
        /// <param name="falseString">字符串</param>
        public static string ToString(this bool @this, string trueString, string falseString) =>
            @this ? trueString : falseString;

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <param name="this">值</param>
        /// <param name="trueString">字符串</param>
        /// <param name="falseString">字符串</param>
        public static string ToString(this bool? @this, string trueString, string falseString) =>
            @this ?? false ? trueString : falseString;

        #endregion

        #region ToChineseString(将布尔值转换为等效中文字符串表示形式)

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="this">值</param>
        public static string ToChineseString(this bool @this) => ToString(@this, "是", "否");

        /// <summary>
        /// 将布尔值转换为等效中文字符串表示形式（true:是、false:否）
        /// </summary>
        /// <param name="this">值</param>
        public static string ToChineseString(this bool? @this) => ToString(@this, "是", "否");

        #endregion
    }
}
