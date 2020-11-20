using Bing.Admin.Systems.Domain.Models;
using Bing.Domain.Entities.Events;

namespace Bing.Admin.Systems.Domain.DomainEvents
{
    /// <summary>
    /// 用户登录领域事件
    /// </summary>
    public class UserLoginDomainEvent : DomainEvent
    {
        /// <summary>
        /// 用户
        /// </summary>
        public User User { get; }

        /// <summary>
        /// IP地址
        /// </summary>
        public string Ip { get; }

        /// <summary>
        /// 客户端代理头
        /// </summary>
        public string UserAgent { get; }

        /// <summary>
        /// 初始化一个<see cref="UserLoginDomainEvent"/>类型的实例
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="ip">IP地址</param>
        /// <param name="userAgent">客户端代理头</param>
        public UserLoginDomainEvent(User user, string ip, string userAgent)
        {
            User = user;
            Ip = ip;
            UserAgent = userAgent;
        }
    }
}
