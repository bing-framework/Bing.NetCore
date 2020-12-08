using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.EventHandlers.Abstractions.Systems;
using Bing.Admin.Infrastructure;
using Bing.Admin.Systems.Domain.Events;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Events;
using Bing.Extensions;
using Bing.Logs.Aspects;
using Bing.Mapping;
using Bing.Security.Claims;
using Bing.Users;

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
        public UserLoginLogMessageEventHandler(IAdminUnitOfWork unitOfWork
            , IUserLoginLogManager userLoginLogManager
            , ICurrentPrincipalAccessor currentPrincipalAccessor
            , ICurrentUser currentUser)
        {
            UnitOfWork = unitOfWork;
            UserLoginLogManager = userLoginLogManager;
            CurrentPrincipalAccessor = currentPrincipalAccessor;
            CurrentUser = currentUser;
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
        /// 当前安全主体访问器
        /// </summary>
        protected ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; }

        /// <summary>
        /// 当前用户
        /// </summary>
        protected ICurrentUser CurrentUser { get; }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="message">消息</param>
        [DebugLog]
        [EventHandler(MessageEventConst.UserLogin, Group = QueueGroupConst.UserLoginLog)]
        public async Task UserLoginAsync(UserLoginMessage message)
        {
            await Task.Delay(5000);
            await UserLoginLogManager.CreateAsync(message.MapTo(new UserLoginLogParameter()));
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 获取安全令牌
        /// </summary>
        private ClaimsPrincipal GetClaimsPrincipal()
        {
            var claims = new List<Claim>();
            //AddClaim(claims,IdentityModel.JwtClaimTypes.Subject, Guid.NewGuid().ToString());
            //AddClaim(claims, IdentityModel.JwtClaimTypes.Name, "Test");
            AddClaim(claims, BingClaimTypes.UserId, Guid.NewGuid().ToString());
            AddClaim(claims, BingClaimTypes.UserName, "Test");
            AddClaim(claims, Bing.Security.Claims.ClaimTypes.FullName, "测试名称");
            AddClaim(claims, BingClaimTypes.PhoneNumber, "123456");
            AddClaim(claims, BingClaimTypes.Email, "test@test.com");
            return new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        /// <summary>
        /// 添加声明
        /// </summary>
        /// <param name="list">列表</param>
        /// <param name="claim">声明</param>
        public void AddClaim(List<Claim> list, Claim claim)
        {
            if (claim == null
                || claim.Value.IsEmpty()
                || list.Exists(x => string.Equals(x.Type.SafeString(), claim.Type.SafeString(), StringComparison.CurrentCultureIgnoreCase)))
                return;
            list.Add(claim);
        }

        /// <summary>
        /// 添加声明
        /// </summary>
        /// <param name="list">列表</param>
        /// <param name="type">类型</param>
        /// <param name="value">值</param>
        public void AddClaim(List<Claim> list, string type, string value)
        {
            if (type.IsEmpty() || value.IsEmpty())
                return;
            AddClaim(list, new Claim(type, value));
        }
    }
}
