namespace Bing.Logging.Core
{
    /// <summary>
    /// 日志事件描述符
    /// </summary>
    public class LogEventDescriptor
    {
        /// <summary>
        /// 初始化一个<see cref="LogEventDescriptor"/>类型的实例
        /// </summary>
        public LogEventDescriptor()
        {
            Context = new LogEventContext();
        }

        /// <summary>
        /// 日志事件上下文
        /// </summary>
        public LogEventContext Context { get; }
    }
}
