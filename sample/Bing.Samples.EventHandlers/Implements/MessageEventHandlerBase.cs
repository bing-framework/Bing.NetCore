using Bing.Samples.EventHandlers.Abstractions;

namespace Bing.Samples.EventHandlers.Implements
{
    /// <summary>
    /// 消息事件处理器基类
    /// </summary>
    public abstract class MessageEventHandlerBase : IMessageEventHandler, DotNetCore.CAP.ICapSubscribe
    {
        /// <summary>
        /// 日志
        /// </summary>
        private Bing.Logs.ILog _log;

        /// <summary>
        /// 日志
        /// </summary>
        public Bing.Logs.ILog Log => _log ?? (_log = GetLog());

        /// <summary>
        /// 获取日志操作
        /// </summary>
        protected virtual Bing.Logs.ILog GetLog()
        {
            try
            {
                return Bing.Logs.Log.GetLog(this);
            }
            catch
            {
                return Bing.Logs.Log.Null;
            }
        }
    }
}
