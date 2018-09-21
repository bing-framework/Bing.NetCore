using System;

namespace Bing.IM.Models.Base
{
    /// <summary>
    /// 聊天消息视图模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class ChatMessageViewModel<TKey>
    {
        /// <summary>
        /// 是否本人发送消息
        /// </summary>
        public bool Self { get; set; }

        /// <summary>
        /// 来源
        /// </summary>
        public TKey From { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 头像
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 添加时间
        /// </summary>
        public DateTime AddTime { get; set; }
    }
}
