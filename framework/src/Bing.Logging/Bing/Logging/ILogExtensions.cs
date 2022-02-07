using System;

namespace Bing.Logging
{
    /// <summary>
    /// 日志操作(<see cref="ILog{TCategoryName}"/>) 扩展
    /// </summary>
    public static class ILogExtensions
    {
        /// <summary>
        /// 消息换行
        /// </summary>
        /// <typeparam name="TCategoryName">日志类别</typeparam>
        /// <param name="log">日志操作</param>
        public static ILog<TCategoryName> Line<TCategoryName>(this ILog<TCategoryName> log)
        {
            log.Message(Environment.NewLine);
            return log;
        }
    }
}
