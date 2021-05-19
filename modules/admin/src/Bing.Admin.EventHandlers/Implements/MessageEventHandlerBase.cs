namespace Bing.Admin.EventHandlers.Implements
{
    /// <summary>
    /// 消息事件处理器基类
    /// </summary>
    public abstract class MessageEventHandlerBase : DotNetCore.CAP.ICapSubscribe
    {
        /// <summary>
        /// 日志
        /// </summary>
        private Bing.Logs.ILog _log;

        /// <summary>
        /// 日志
        /// </summary>
        public Bing.Logs.ILog Log => _log ??= Logs.Log.GetLog(this);
    }
}
