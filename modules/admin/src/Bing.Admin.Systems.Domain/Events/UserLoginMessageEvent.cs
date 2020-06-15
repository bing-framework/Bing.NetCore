using System;

namespace Bing.Admin.Systems.Domain.Events
{
    public class UserLoginMessageEvent
    {
    }

    /// <summary>
    /// 用户登录消息
    /// </summary>
    public class UserLoginMessage
    {
        /// <summary>
        /// IP地址
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 客户端代理头
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// 用户编号
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; }
    }
}
