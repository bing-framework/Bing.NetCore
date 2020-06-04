using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Domain.Shared.Results;
using Bing.Admin.Service.Abstractions;
using Bing.Admin.Service.Requests.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Applications;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Permissions.Identity.Results;
using IdentityModel;

namespace Bing.Admin.Service.Implements
{
    /// <summary>
    /// 安全服务
    /// </summary>
    public class SecurityService : ServiceBase, ISecurityService
    {
        /// <summary>
        /// 初始化一个<see cref="SecurityService"/>类型的实例
        /// </summary>
        public SecurityService(IAdminUnitOfWork unitOfWork
            , IUserRepository userRepository
            , IApplicationRepository applicationRepository)
        {
            UnitOfWork = unitOfWork;
            UserRepository = userRepository;
            ApplicationRepository = applicationRepository;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; }

        /// <summary>
        /// 应用程序仓储
        /// </summary>
        protected IApplicationRepository ApplicationRepository { get; }

        public async Task<SignInWithTokenResult> SignInAsync(AdminLoginRequest request)
        {
            var user = await GetUserAsync(request.UserName);
            if (user == null)
                return new SignInWithTokenResult {Message = "用户名不存在", State = SignInState.Failed};
            await AddClaimsToUserAsync(user, ApplicationCode.Admin);
            return null;
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
            //await AddRoleClaimsAsync(user, applicationCode);
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
            user.AddClaim(Bing.Security.Claims.ClaimTypes.ApplicationId, application.Id.SafeString());
            user.AddClaim(Bing.Security.Claims.ClaimTypes.ApplicationCode, applicationCode);
            user.AddClaim(Bing.Security.Claims.ClaimTypes.ApplicationName, application.Name);
            user.AddClaim(JwtClaimTypes.ClientId, application.Id.SafeString());
        }
    }
}
