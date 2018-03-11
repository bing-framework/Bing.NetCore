using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Properties;

namespace Bing.Exceptions
{
    /// <summary>
    /// 并发异常
    /// </summary>
    public class ConcurrencyException : Warning
    {
        /// <summary>
        /// 初始化一个<see cref="ConcurrencyException"/>类型的实例
        /// </summary>
        public ConcurrencyException() : this("")
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConcurrencyException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        public ConcurrencyException(string message) : this(message, null)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConcurrencyException"/>类型的实例
        /// </summary>
        /// <param name="exception">异常</param>
        public ConcurrencyException(Exception exception) : this("", exception)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConcurrencyException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常</param>
        public ConcurrencyException(string message, Exception exception) : this(message, exception, "")
        {
        }

        /// <summary>
        /// 初始化一个<see cref="ConcurrencyException"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常</param>
        /// <param name="code">错误码</param>
        public ConcurrencyException(string message, Exception exception, string code) : base(
            "并发异常:" + LibraryResource.ConcurrencyExceptionMessage + Environment.NewLine + message, code, exception)
        {
        }
    }
}
