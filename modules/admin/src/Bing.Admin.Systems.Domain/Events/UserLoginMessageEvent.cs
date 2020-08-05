using System;
using Bing.Admin.Infrastructure;
using Bing.Events;
using Bing.Events.Messages;

namespace Bing.Admin.Systems.Domain.Events
{
    /// <summary>
    /// 用户登录 消息事件
    /// </summary>
    public class UserLoginMessageEvent : MessageEvent<UserLoginMessage>
    {
        /// <summary>
        /// 初始化一个<see cref="UserLoginMessageEvent"/>类型的实例
        /// </summary>
        /// <param name="data">数据</param>
        public UserLoginMessageEvent(UserLoginMessage data) : base(data)
        {
            Send = true;
            Name = MessageEventConst.UserLogin;
        }
    }

    /// <summary>
    /// 用户登录消息
    /// </summary>
    public class UserLoginMessage:IEventSession
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

        /// <summary>
        /// 会话标识
        /// </summary>
        public string SessionId { get; set; }
    }
}
