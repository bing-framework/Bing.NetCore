using System;
using Bing.Exceptions;
using Bing.Logging;
using Microsoft.Extensions.Logging;

namespace Bing.Authorization
{
    /// <summary>
    /// 授权异常。未经授权的请求将引发此异常
    /// </summary>
    [Serializable]
    public class BingAuthorizationException : Warning, IHasLogLevel
    {
        /// <summary>
        /// 初始化一个<see cref="Warning"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        public BingAuthorizationException(string message) : base(message)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="Warning"/>类型的实例
        /// </summary>
        /// <param name="exception">异常</param>
        public BingAuthorizationException(Exception exception) : base(exception)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="Warning"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="code">错误码</param>
        public BingAuthorizationException(string message, string code) : base(message, code)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="Warning"/>类型的实例
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="code">错误码</param>
        /// <param name="exception">异常</param>
        public BingAuthorizationException(string message, string code, Exception exception) : base(message, code, exception)
        {
        }

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Warning;
    }
}
