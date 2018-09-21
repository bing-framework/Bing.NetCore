using System;

namespace Bing.IM.Models.Base
{
    /// <summary>
    /// IM消息模型
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    // ReSharper disable once InconsistentNaming
    public class IMMessageModel<TKey>
    {
        /// <summary>
        /// 来源
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// 接收
        /// </summary>
        public string To { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 消息类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 添加时间
        /// </summary>
        public DateTime AddTime { get; set; }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsVlid => !(string.IsNullOrEmpty(From) || string.IsNullOrEmpty(To) || Content == null);

        /// <summary>
        /// 房间标识
        /// </summary>
        public string RoomId { get; set; }
    }
}
