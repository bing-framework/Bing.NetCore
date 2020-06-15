using System;
using Bing.Admin.Data.Repositories.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Implements;
using Bing.Permissions.Identity.Describers;
using Bing.Permissions.Identity.Extensions;
using Bing.Permissions.Identity.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Service.Extensions
{
    /// <summary>
    /// 扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册权限服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="setupAction">配置操作</param>
        /// <returns></returns>
        public static IServiceCollection AddPermission(this IServiceCollection services,
            Action<PermissionOptions> setupAction = null)
        {
            var permissionOptions = new PermissionOptions();
            setupAction?.Invoke(permissionOptions);
            services.Configure(setupAction);
            services.AddScoped<IdentityUserManager>();
            services.AddScoped<IdentitySignInManager>();
            services.AddIdentity<User, Role>(options => options.Load(permissionOptions))
                .AddUserStore<UserRepository>()
                .AddRoleStore<RoleRepository>()
                .AddDefaultTokenProviders();
            services.AddScoped<IdentityErrorDescriber, IdentityErrorChineseDescriber>();
            services.AddLogging();
            return services;
        }
    }
}
