using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.DomainEvents;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Domain.Entities.Events;

namespace Bing.Admin.EventHandlers.Implements.Systems
{
    /// <summary>
    /// 用户登录领域事件处理器
    /// </summary>
    public class UserLoginDomainEventHandler : IDomainEventHandler<UserLoginDomainEvent>
    {
        /// <summary>
        /// 用户登录日志管理
        /// </summary>
        protected IUserLoginLogManager UserLoginLogManager { get; }

        /// <summary>
        /// 初始化一个<see cref="UserLoginDomainEventHandler"/>类型的实例
        /// </summary>
        public UserLoginDomainEventHandler(IUserLoginLogManager userLoginLogManager)
        {
            UserLoginLogManager = userLoginLogManager;
        }

        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="event">领域事件</param>
        public async Task HandleAsync(UserLoginDomainEvent @event)
        {
            await UserLoginLogManager.CreateAsync(new UserLoginLogParameter
            {
                UserId = @event.User.Id, Name = @event.User.Nickname, Ip = @event.Ip, UserAgent = @event.UserAgent
            });
        }
    }
}
