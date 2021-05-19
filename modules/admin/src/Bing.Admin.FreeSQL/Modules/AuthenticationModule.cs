using System;
using System.ComponentModel;
using Bing.Admin.Data.Repositories.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Implements;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Permissions.Extensions;
using Bing.Permissions.Identity.Describers;
using Bing.Permissions.Identity.Extensions;
using Bing.Permissions.Identity.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// 身份认证模块
    /// </summary>
    [Description("身份认证模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class AuthenticationModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 1;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            var configuration = services.GetConfiguration();
            // 添加权限服务
            AddPermission(services, o =>
             {
                 o.Store.StoreOriginalPassword = true;
                 o.Password.MinLength = 6;
             });
            // 添加Jwt认证
            services.AddJwt(configuration);
            return services;
        }

        /// <summary>
        /// 注册权限服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="setupAction">配置操作</param>
        public IServiceCollection AddPermission(IServiceCollection services,
            Action<PermissionOptions> setupAction = null)
        {
            var permissionOptions = new PermissionOptions();
            setupAction?.Invoke(permissionOptions);
            services.Configure(setupAction);
            services.AddScoped<IdentityUserManager>();
            //services.AddScoped<IdentitySignInManager>();
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
