namespace Bing.Events
{
    /// <summary>
    /// 事件会话
    /// </summary>
    public interface IEventSession
    {
        /// <summary>
        /// 会话标识
        /// </summary>
        string SessionId { get; set; }
    }
}
