using System.ComponentModel;

namespace Bing.IM.Models.Messages
{
    /// <summary>
    /// IM消息类型
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public enum IMMessageType
    {
        /// <summary>
        /// 系统消息
        /// </summary>
        [Description("系统消息")]
        Sysyem = 0,

        /// <summary>
        /// 点对点消息
        /// </summary>
        [Description("点对点消息")]
        ClientToClient = 1,

        /// <summary>
        /// 群组消息
        /// </summary>
        [Description("群组消息")]
        ClientToGroup = 2
    }
}
