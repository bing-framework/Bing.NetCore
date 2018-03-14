using System;
using System.Collections.Generic;
using System.IO;
using Bing.Utils.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 系统扩展 - 参数检查
    /// </summary>
    public static partial class Extensions
    {
        #region Required(断言)
        /// <summary>
        /// 验证指定值的断言表达式是否为真，不为值抛出<see cref="Exception"/>异常
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="assertionFunc">要验证的断言</param>
        /// <param name="message">异常消息</param>
        public static void Required<T>(this T value, Func<T, bool> assertionFunc, string message)
        {
            Check.Required<T>(value, assertionFunc, message);
        }

        /// <summary>
        /// 验证指定值的断言表达式是否为真，不为真抛出<see cref="Exception"/>异常
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="assertionFunc">要验证的断言</param>
        /// <param name="message">异常消息</param>
        public static void Required<T, TException>(this T value, Func<T, bool> assertionFunc, string message)
            where TException : Exception
        {
            Check.Required<T, TException>(value, assertionFunc, message);
        }

        #endregion

        #region CheckNotNull(不可空检查)

        /// <summary>
        /// 检查参数不能为空引用，否则抛出<see cref="ArgumentNullException"/>异常
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        public static void CheckNotNull<T>(this T value, string paramName) where T : class
        {
            Check.NotNull<T>(value, paramName);
        }

        /// <summary>
        /// 检查字符串不能为空引用或空字符串，否则抛出<see cref="ArgumentNullException"/>异常或<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        public static void CheckNotNullOrEmpty(this string value, string paramName)
        {
            Check.NotNullOrEmpty(value, paramName);
        }

        /// <summary>
        /// 检查Guid值不能为Guid.Empty，否则抛出<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        public static void CheckNotEmpty(this Guid value, string paramName)
        {
            Check.NotEmpty(value, paramName);
        }

        /// <summary>
        /// 检查集合不能为空引用或空集合，否则抛出<see cref="ArgumentNullException"/>异常或<see cref="ArgumentException"/>异常。
        /// </summary>
        /// <typeparam name="T">集合项的类型</typeparam>
        /// <param name="collection">要判断的值</param>
        /// <param name="paramName">参数名</param>
        public static void CheckNotNullOrEmpty<T>(this IEnumerable<T> collection, string paramName)
        {
            Check.NotNullOrEmpty<T>(collection, paramName);
        }

        #endregion

        #region CheckBetween(范围检查)

        /// <summary>
        /// 检查参数必须小于[或可等于，参数canEqual]指定值，否则抛出<see cref="ArgumentOutOfRangeException"/>异常
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <param name="target">要比较的值</param>
        /// <param name="canEqual">是否可等于</param>
        public static void CheckLessThan<T>(this T value, string paramName, T target, bool canEqual = false)
            where T : IComparable<T>
        {
            Check.LessThan<T>(value, paramName, target, canEqual);
        }

        /// <summary>
        /// 检查参数必须大于[或可等于，参数canEqual]指定值，否则抛出<see cref="ArgumentOutOfRangeException"/>异常
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <param name="target">要比较的值</param>
        /// <param name="canEqual">是否可等于</param>
        public static void CheckGreaterThan<T>(this T value, string paramName, T target, bool canEqual = false)
            where T : IComparable<T>
        {
            Check.GreaterThan<T>(value, paramName, target, canEqual);
        }

        /// <summary>
        /// 检查参数必须在指定范围之间，否则抛出<see cref="ArgumentOutOfRangeException"/>异常
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <param name="start">比较范围的起始值</param>
        /// <param name="end">比较范围的结束值</param>
        /// <param name="startEqual">是否可等于起始值</param>
        /// <param name="endEqual">是否可等于结束值</param>
        public static void CheckBetween<T>(this T value, string paramName, T start, T end, bool startEqual = false,
            bool endEqual = false) where T : IComparable<T>
        {
            Check.Between<T>(value, paramName, start, end, startEqual, endEqual);
        }

        #endregion

        #region CheckIO(文件检查)

        /// <summary>
        /// 检查指定路径的文件夹必须存在，否则抛出<see cref="DirectoryNotFoundException"/>异常
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <param name="paramName">参数名</param>
        public static void CheckDirectoryExists(this string directory, string paramName = null)
        {
            Check.DirectoryExists(directory, paramName);
        }

        /// <summary>
        /// 检查指定路径的文件必须存在，否则抛出<see cref="FileNotFoundException"/>异常。
        /// </summary>
        /// <param name="fileName">文件路径，包含文件名</param>
        /// <param name="paramName">参数名</param>
        public static void CheckFileExists(this string fileName, string paramName = null)
        {
            Check.FileExists(fileName, paramName);
        }

        #endregion
    }
}
