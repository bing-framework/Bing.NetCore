using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Domain.Shared.Results;
using Bing.Admin.Infrastructure;
using Bing.Admin.Service.Abstractions;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Systems.Domain.Events;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Events.Messages;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Permissions.Identity.JwtBearer;
using Bing.Permissions.Identity.Results;
using Bing.Security.Claims;

namespace Bing.Admin.Service.Implements
{
    /// <summary>
    /// 安全服务
    /// </summary>
    public class SecurityService : Bing.Application.Services.AppServiceBase, ISecurityService
    {
        /// <summary>
        /// 初始化一个<see cref="SecurityService"/>类型的实例
        /// </summary>
        public SecurityService(IAdminUnitOfWork unitOfWork
            , IJsonWebTokenBuilder tokenBuilder
            , IMessageEventBus messageEventBus
            , IUserRepository userRepository
            , IApplicationRepository applicationRepository
            , IRoleRepository roleRepository
            , ISignInManager signInManager)
        {
            UnitOfWork = unitOfWork;
            TokenBuilder = tokenBuilder;
            MessageEventBus = messageEventBus;
            UserRepository = userRepository;
            ApplicationRepository = applicationRepository;
            RoleRepository = roleRepository;
            SignInManager = signInManager;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// Jwt令牌构建器
        /// </summary>
        protected IJsonWebTokenBuilder TokenBuilder { get; }

        /// <summary>
        /// 消息事件总线
        /// </summary>
        protected IMessageEventBus MessageEventBus { get; }

        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; }

        /// <summary>
        /// 应用程序仓储
        /// </summary>
        protected IApplicationRepository ApplicationRepository { get; }
        
        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; }

        /// <summary>
        /// 登录管理器
        /// </summary>
        protected ISignInManager SignInManager { get; }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="request">请求</param>
        public async Task<SignInWithTokenResult> SignInAsync(AdminLoginRequest request)
        {
            var user = await GetUserAsync(request.UserName);
            if (user == null)
                return new SignInWithTokenResult {Message = "用户名不存在", State = SignInState.Failed};
            await AddClaimsToUserAsync(user, ApplicationCode.Admin);
            var result = await SignInManager.SignInAsync(user, request.Password);
            user.AddLoginLog(Web.IP,Web.Browser);
            await MessageEventBus.PublishAsync(new UserLoginMessageEvent(new UserLoginMessage
            {
                UserId = user.Id,
                Name = user.Nickname,
                Ip = Web.IP,
                UserAgent = Web.Browser
            },false));
            await UnitOfWork.CommitAsync();
            return await GetLoginResultAsync(user, result, ApplicationCode.Admin);
        }

        /// <summary>
        /// 获取用户
        /// </summary>
        /// <param name="userName">用户名</param>
        private async Task<User> GetUserAsync(string userName) => await UserRepository.SingleAsync(x => x.UserName == userName);

        /// <summary>
        /// 添加声明到用户
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="applicationCode">应用程序编码</param>
        private async Task AddClaimsToUserAsync(User user, string applicationCode)
        {
            user.AddUserClaims();
            await AddApplicationClaims(user, applicationCode);
            await AddRoleClaimsAsync(user, applicationCode);
        }

        /// <summary>
        /// 添加应用程序声明
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="applicationCode">应用程序编码</param>
        private async Task AddApplicationClaims(User user, string applicationCode)
        {
            var application = await ApplicationRepository.GetByCodeAsync(applicationCode);
            if (application == null)
                throw new Warning("无效应用程序");
            //user.AddClaim(Bing.Security.Claims.ClaimTypes.ApplicationId, application.Id.SafeString());
            //user.AddClaim(Bing.Security.Claims.ClaimTypes.ApplicationCode, applicationCode);
            //user.AddClaim(Bing.Security.Claims.ClaimTypes.ApplicationName, application.Name);
            //user.AddClaim(JwtClaimTypes.ClientId, application.Id.SafeString());
            user.AddClaim(BingClaimTypes.ApplicationId, application.Id.SafeString());
            user.AddClaim(BingClaimTypes.ApplicationCode, applicationCode);
            user.AddClaim(BingClaimTypes.ApplicationName, application.Name);
            user.AddClaim(JwtClaimTypes.ClientId, application.Id.SafeString());
        }

        /// <summary>
        /// 添加角色声明
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="applicationCode">应用程序编码</param>
        private async Task AddRoleClaimsAsync(User user, string applicationCode)
        {
            var roles = await RoleRepository.GetRolesAsync(user.Id);
            if (!roles.Any())
                throw new Warning($"用户：{user.UserName} 未设置任何权限，不能访问系统，请联系管理员");
            user.AddClaim(Bing.Security.Claims.BingClaimTypes.RoleIds, roles.Select(x => x.Id).Join());
            user.AddClaim(JwtClaimTypes.RoleCode, roles.Select(x => x.Code).Join());
            user.AddClaim(Bing.Security.Claims.BingClaimTypes.RoleNames, roles.Select(x => x.Name).Join());
        }

        /// <summary>
        /// 获取登录结果
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="signInResult">登录结果</param>
        /// <param name="applicationCode">应用程序编码</param>
        private async Task<SignInWithTokenResult> GetLoginResultAsync(User user, SignInResult signInResult, string applicationCode)
        {
            if (signInResult.State == SignInState.Failed)
                return new SignInWithTokenResult { UserId = signInResult.UserId, State = signInResult.State, Message = signInResult.Message };
            //await MessageEventBus.PublishAsync(new UserLoginMessageEvent(new UserLoginMessage
            //{
            //    UserId = user.Id,
            //    Name = user.Nickname,
            //    Ip = Web.IP,
            //    UserAgent = Web.Browser
            //}));
            var result = await TokenBuilder.CreateAsync(user.GetClaims().ToDictionary(x => x.Type, x => x.Value));
            return new SignInWithTokenResult
            {
                UserId = signInResult.UserId,
                State = signInResult.State,
                Message = signInResult.Message,
                Token = result
            };
        }

        /// <summary>
        /// 刷新令牌
        /// </summary>
        /// <param name="refreshToken">刷新令牌</param>
        public async Task<JsonWebToken> RefreshTokenAsync(string refreshToken)
        {
            var result = await TokenBuilder.RefreshAsync(refreshToken);
            return result;
        }
    }
}
