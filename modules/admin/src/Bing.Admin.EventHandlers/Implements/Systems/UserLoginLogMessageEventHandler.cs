using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.EventHandlers.Abstractions.Systems;
using Bing.Admin.Infrastructure;
using Bing.Admin.Systems.Domain.Events;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Events;
using Bing.Mapping;

namespace Bing.Admin.EventHandlers.Implements.Systems
{
    /// <summary>
    /// 用户登录日志 消息事件处理器
    /// </summary>
    public class UserLoginLogMessageEventHandler : MessageEventHandlerBase, IUserLoginLogMessageEventHandler
    {
        /// <summary>
        /// 初始化一个<see cref="UserLoginLogMessageEventHandler"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userLoginLogManager">用户登录日志管理</param>
        public UserLoginLogMessageEventHandler(IAdminUnitOfWork unitOfWork, IUserLoginLogManager userLoginLogManager)
        {
            UnitOfWork = unitOfWork;
            UserLoginLogManager = userLoginLogManager;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// 用户登录日志管理
        /// </summary>
        protected IUserLoginLogManager UserLoginLogManager { get; }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="message">消息</param>
        [EventHandler(MessageEventConst.UserLogin, Group = QueueGroupConst.UserLoginLog)]
        public async Task UserLoginAsync(UserLoginMessage message)
        {
            await UserLoginLogManager.CreateAsync(message.MapTo(new UserLoginLogParameter()));
            await UnitOfWork.CommitAsync();
        }
    }
}
