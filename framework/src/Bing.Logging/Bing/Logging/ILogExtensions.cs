using System;

namespace Bing.Logging
{
    /// <summary>
    /// 日志操作(<see cref="ILog{TCategoryName}"/>) 扩展
    /// </summary>
    public static class ILogExtensions
    {
        /// <summary>
        /// 添加消息
        /// </summary>
        /// <typeparam name="TCategoryName">日志类别</typeparam>
        /// <param name="log">日志操作</param>
        /// <param name="message">消息</param>
        /// <param name="args">日志消息参数</param>
        public static ILog<TCategoryName> Append<TCategoryName>(this ILog<TCategoryName> log, string message, params object[] args)
        {
            if (log is null)
                throw new ArgumentNullException(nameof(log));
            log.Message(message, args);
            return log;
        }

        /// <summary>
        /// 添加消息，当条件为true时
        /// </summary>
        /// <typeparam name="TCategoryName">日志类别</typeparam>
        /// <param name="log">日志操作</param>
        /// <param name="message">消息</param>
        /// <param name="condition">条件，值为true时，则添加消息</param>
        /// <param name="args">日志消息参数</param>
        public static ILog<TCategoryName> AppendIf<TCategoryName>(this ILog<TCategoryName> log, string message, bool condition, params object[] args)
        {
            if (log is null)
                throw new ArgumentNullException(nameof(log));
            if (condition)
                log.Message(message, args);
            return log;
        }

        /// <summary>
        /// 添加消息并换行
        /// </summary>
        /// <typeparam name="TCategoryName">日志类别</typeparam>
        /// <param name="log">日志操作</param>
        /// <param name="message">消息</param>
        /// <param name="args">日志消息参数</param>
        public static ILog<TCategoryName> AppendLine<TCategoryName>(this ILog<TCategoryName> log, string message, params object[] args)
        {
            if (log is null)
                throw new ArgumentNullException(nameof(log));
            log.Message(message, args);
            log.Message(Environment.NewLine);
            return log;
        }

        /// <summary>
        /// 添加消息并换行，当条件为true时
        /// </summary>
        /// <typeparam name="TCategoryName">日志类别</typeparam>
        /// <param name="log">日志操作</param>
        /// <param name="message">消息</param>
        /// <param name="condition">条件，值为true时，则添加消息</param>
        /// <param name="args">日志消息参数</param>
        /// <returns></returns>
        public static ILog<TCategoryName> AppendLineIf<TCategoryName>(this ILog<TCategoryName> log, string message, bool condition, params object[] args)
        {
            if (log is null)
                throw new ArgumentNullException(nameof(log));
            if (condition)
            {
                log.Message(message, args);
                log.Message(Environment.NewLine);
            }
            return log;
        }

        /// <summary>
        /// 消息换行
        /// </summary>
        /// <typeparam name="TCategoryName">日志类别</typeparam>
        /// <param name="log">日志操作</param>
        public static ILog<TCategoryName> Line<TCategoryName>(this ILog<TCategoryName> log)
        {
            if (log is null)
                throw new ArgumentNullException(nameof(log));
            log.Message(Environment.NewLine);
            return log;
        }
    }
}
