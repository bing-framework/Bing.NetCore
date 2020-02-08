using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.Utils.Properties;

namespace Bing.Helpers
{
    /// <summary>
    /// 参数检查 操作
    /// </summary>
    public static partial class Check
    {
        #region Required(断言)

        /// <summary>
        /// 验证指定值的断言<paramref name="assertion"/>是否为真，如果不为真，抛出指定消息<paramref name="message"/>的指定类型<typeparamref name="TException"/>异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="assertion">要验证的断言</param>
        /// <param name="message">异常消息</param>
        /// <exception cref="ArgumentNullException"></exception>
        private static void Require<TException>(bool assertion, string message) where TException : Exception
        {
            if (assertion)
                return;
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentNullException(nameof(message));
            var exception = (TException)Activator.CreateInstance(typeof(TException), message);
            throw exception;
        }

        /// <summary>
        /// 验证指定值的断言表达式是否为真，不为值抛出<see cref="Exception"/>异常
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="assertionFunc">要验证的断言</param>
        /// <param name="message">异常消息</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Required<T>(T value, Func<T, bool> assertionFunc, string message)
        {
            if (assertionFunc == null)
                throw new ArgumentNullException(nameof(assertionFunc));
            Require<Exception>(assertionFunc(value), message);
        }

        /// <summary>
        /// 验证指定值的断言表达式是否为真，不为真抛出<see cref="Exception"/>异常
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="assertionFunc">要验证的断言</param>
        /// <param name="message">异常消息</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Required<T, TException>(T value, Func<T, bool> assertionFunc, string message)
            where TException : Exception
        {
            if (assertionFunc == null)
                throw new ArgumentNullException(nameof(assertionFunc));
            Require<TException>(assertionFunc(value), message);
        }

        #endregion

        #region NotNull(不可空检查)

        /// <summary>
        /// 检查参数不能为空引用，否则抛出<see cref="ArgumentNullException"/>异常
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void NotNull<T>(T value, string paramName) => Require<ArgumentNullException>(value != null, string.Format(R.ParameterCheck_NotNull, paramName));

        /// <summary>
        /// 检查字符串不能为空引用或空字符串，否则抛出<see cref="ArgumentNullException"/>异常或<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static void NotNullOrEmpty(string value, string paramName)
        {
            NotNull(value, paramName);
            Require<ArgumentException>(!string.IsNullOrEmpty(value), string.Format(R.ParameterCheck_NotNullOrEmpty_String, paramName));
        }

        /// <summary>
        /// 检查Guid值不能为Guid.Empty，否则抛出<see cref="ArgumentException"/>异常
        /// </summary>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void NotEmpty(Guid value, string paramName) => Require<ArgumentException>(value != Guid.Empty, string.Format(R.ParameterCheck_NotEmpty_Guid, paramName));

        /// <summary>
        /// 检查集合不能为空引用或空集合，否则抛出<see cref="ArgumentNullException"/>异常或<see cref="ArgumentException"/>异常。
        /// </summary>
        /// <typeparam name="T">集合项的类型</typeparam>
        /// <param name="collection">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void NotNullOrEmpty<T>(IEnumerable<T> collection, string paramName)
        {
            NotNull(collection, paramName);
            Require<ArgumentException>(collection.Any(), string.Format(R.ParameterCheck_NotNullOrEmpty_Collection, paramName));
        }

        /// <summary>
        /// 检查字典不能为空引用或空字典，否则抛出<see cref="ArgumentNullException"/>异常或<see cref="ArgumentException"/>异常。
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static void NotNullOrEmpty<T>(IDictionary<string, T> dictionary, string paramName)
        {
            NotNull(dictionary, paramName);
            Require<ArgumentException>(dictionary.Any(), string.Format(R.ParameterCheck_NotNullOrEmpty_Collection));
        }

        #endregion

        #region Between(范围检查)

        /// <summary>
        /// 检查参数必须小于[或可等于，参数canEqual]指定值，否则抛出<see cref="ArgumentOutOfRangeException"/>异常
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <param name="target">要比较的值</param>
        /// <param name="canEqual">是否可等于</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void LessThan<T>(T value, string paramName, T target, bool canEqual = false)
            where T : IComparable<T>
        {
            bool flag = canEqual ? value.CompareTo(target) <= 0 : value.CompareTo(target) < 0;
            string format = canEqual ? R.ParameterCheck_NotLessThanOrEqual : R.ParameterCheck_NotLessThan;
            Require<ArgumentOutOfRangeException>(flag, string.Format(format, paramName, target));
        }

        /// <summary>
        /// 检查参数必须大于[或可等于，参数canEqual]指定值，否则抛出<see cref="ArgumentOutOfRangeException"/>异常
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="value">要判断的值</param>
        /// <param name="paramName">参数名</param>
        /// <param name="target">要比较的值</param>
        /// <param name="canEqual">是否可等于</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void GreaterThan<T>(T value, string paramName, T target, bool canEqual = false)
            where T : IComparable<T>
        {
            bool flag = canEqual ? value.CompareTo(target) >= 0 : value.CompareTo(target) > 0;
            string format = canEqual ? R.ParameterCheck_NotGreaterThanOrEqual : R.ParameterCheck_NotGreaterThan;
            Require<ArgumentOutOfRangeException>(flag, string.Format(format, paramName, target));
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
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Between<T>(T value, string paramName, T start, T end, bool startEqual = false,
            bool endEqual = false) where T : IComparable<T>
        {
            bool flag = startEqual ? value.CompareTo(start) >= 0 : value.CompareTo(start) > 0;
            string message = startEqual
                ? string.Format(R.ParameterCheck_Between, paramName, start, end)
                : string.Format(R.ParameterCheck_BetweenNotEqual, paramName, start, end, start);
            Require<ArgumentOutOfRangeException>(flag, message);

            flag = endEqual ? value.CompareTo(end) <= 0 : value.CompareTo(end) < 0;
            message = endEqual
                ? string.Format(R.ParameterCheck_Between, paramName, start, end)
                : string.Format(R.ParameterCheck_BetweenNotEqual, paramName, start, end, end);
            Require<ArgumentOutOfRangeException>(flag, message);
        }

        /// <summary>
        /// 检查参数不能为负数或零，否则抛出<see cref="ArgumentOutOfRangeException"/>异常
        /// </summary>
        /// <param name="timeSpan">时间戳</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void NotNegativeOrZero(TimeSpan timeSpan, string paramName) => Require<ArgumentOutOfRangeException>(timeSpan > TimeSpan.Zero, paramName);

        #endregion

        #region IO(文件检查)

        /// <summary>
        /// 检查指定路径的文件夹必须存在，否则抛出<see cref="DirectoryNotFoundException"/>异常
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="DirectoryNotFoundException"></exception>
        public static void DirectoryExists(string directory, string paramName = null)
        {
            NotNull(directory, paramName);
            Require<DirectoryNotFoundException>(Directory.Exists(directory), string.Format(R.ParameterCheck_DirectoryNotExists, directory));
        }

        /// <summary>
        /// 检查指定路径的文件必须存在，否则抛出<see cref="FileNotFoundException"/>异常。
        /// </summary>
        /// <param name="fileName">文件路径，包含文件名</param>
        /// <param name="paramName">参数名</param>
        /// <exception cref="FileNotFoundException"></exception>
        public static void FileExists(string fileName, string paramName = null)
        {
            NotNull(fileName, paramName);
            Require<FileNotFoundException>(File.Exists(fileName), string.Format(R.ParameterCheck_FileNotExists, fileName));
        }

        #endregion
    }
}
