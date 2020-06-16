using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Events;

namespace Bing.Admin.EventHandlers.Abstractions.Systems
{
    /// <summary>
    /// 用户登录日志 事件处理器
    /// </summary>
    public interface IUserLoginLogMessageEventHandler
    {
        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="message">消息</param>
        Task UserLoginAsync(UserLoginMessage message);
    }
}
