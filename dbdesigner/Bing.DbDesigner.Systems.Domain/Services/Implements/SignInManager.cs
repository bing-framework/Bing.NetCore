using System;
using System.Linq;
using System.Threading.Tasks;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.DbDesigner.Systems.Domain.Services.Abstractions;
using Bing.Exceptions;
using Bing.Security.Claims;
using Bing.Security.Identity.Services.Abstractions;
using Bing.Security.Identity.Services.Implements;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 登录管理
    /// </summary>
    public class SignInManager:SignInManager<User,Guid>,ISignInManager
    {
        /// <summary>
        /// 应用程序仓储
        /// </summary>
        protected IApplicationRepository ApplicationRepository { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="SignInManager"/>类型的实例
        /// </summary>
        /// <param name="identitySignInManager">Identity登录管理</param>
        /// <param name="userManager">用户管理</param>
        /// <param name="applicationRepository">应用程序仓储</param>
        /// <param name="roleRepository">角色仓储</param>
        public SignInManager(
            IdentitySignInManager identitySignInManager
            , IUserManager userManager
            , IApplicationRepository applicationRepository
            , IRoleRepository roleRepository) : base(identitySignInManager, userManager)
        {
            ApplicationRepository = applicationRepository;
            RoleRepository = roleRepository;
        }

        /// <summary>
        /// 添加声明到用户
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        protected override async Task AddClaimsToUser(User user, string applicationCode)
        {
            await AddApplicationClasm(user, applicationCode);
            await AddRoleClaims(user);
        }

        /// <summary>
        /// 添加应用程序声明
        /// </summary>
        /// <param name="user">用户</param>
        /// <param name="applicationCode">应用程序编码</param>
        /// <returns></returns>
        private async Task AddApplicationClasm(User user, string applicationCode)
        {
            var application = await ApplicationRepository.GetByCodeAsync(applicationCode);
            if (application == null)
            {
                throw new Warning(SystemResource.InvalidApplication);
            }

            user.AddClaim(ClaimTypes.ApplicationId, application.Id.SafeString());
            user.AddClaim(ClaimTypes.ApplicationCode, application.Code);
            user.AddClaim(ClaimTypes.ApplicationName, application.Name);
        }

        /// <summary>
        /// 添加角色声明
        /// </summary>
        /// <param name="user">用户</param>
        /// <returns></returns>
        private async Task AddRoleClaims(User user)
        {
            var roles = await RoleRepository.GetRolesAsync(user.Id);
            user.AddClaim(ClaimTypes.RoleIds, roles.Select(x => x.Id).ToList().Join());
            user.AddClaim(ClaimTypes.RoleName, roles.Select(x => x.Name).ToList().Join());
        }
    }
}
