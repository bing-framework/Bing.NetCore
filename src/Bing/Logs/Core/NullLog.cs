using System;
using System.Collections.Generic;
using System.Text;
using Bing.Logs.Abstractions;

namespace Bing.Logs.Core
{
    /// <summary>
    /// 空日志操作
    /// </summary>
    public class NullLog : ILog
    {
        #region 属性
        /// <summary>
        /// 日志操作实例
        /// </summary>
        public static readonly ILog Instance = new NullLog();

        /// <summary>
        /// 调试级别是否启用
        /// </summary>
        public bool IsDebugEnabled => false;

        /// <summary>
        /// 跟踪级别是否启用
        /// </summary>
        public bool IsTraceEnabled => false;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name => string.Empty;

        #endregion        

        /// <summary>
        /// 设置内容
        /// </summary>
        /// <typeparam name="TContent">日志内容类型</typeparam>
        /// <param name="action">设置内容操作</param>
        /// <returns></returns>
        public ILog Set<TContent>(Action<TContent> action) where TContent : ILogContent
        {
            return this;
        }

        /// <summary>
        /// 获取内容
        /// </summary>
        /// <typeparam name="TContent">日志内容类型</typeparam>
        /// <returns></returns>
        public TContent Get<TContent>() where TContent : ILogContent
        {
            return default(TContent);
        }

        /// <summary>
        /// 跟踪
        /// </summary>
        public void Trace()
        {
        }

        /// <summary>
        /// 跟踪
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Trace(string message)
        {
        }

        /// <summary>
        /// 调试
        /// </summary>
        public void Debug()
        {
        }

        /// <summary>
        /// 调试
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Debug(string message)
        {
        }

        /// <summary>
        /// 信息
        /// </summary>
        public void Info()
        {
        }

        /// <summary>
        /// 信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Info(string message)
        {
        }

        /// <summary>
        /// 警告
        /// </summary>
        public void Warn()
        {
        }

        /// <summary>
        /// 警告
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Warn(string message)
        {
        }

        /// <summary>
        /// 错误
        /// </summary>
        public void Error()
        {
        }

        /// <summary>
        /// 错误
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Error(string message)
        {
        }

        /// <summary>
        /// 致命错误
        /// </summary>
        public void Fatal()
        {
        }

        /// <summary>
        /// 致命错误
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Fatal(string message)
        {
        }
    }
}
