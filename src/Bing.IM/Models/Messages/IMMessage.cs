namespace Bing.IM.Models.Messages
{
    /// <summary>
    /// IM消息
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    // ReSharper disable once InconsistentNaming
    public class IMMessage<TKey>
    {
        /// <summary>
        /// 标识
        /// </summary>
        public TKey Id { get; set; }

        /// <summary>
        /// 头像
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        /// 消息类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }
    }
}
