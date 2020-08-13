using System.ComponentModel;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Admin.EventHandlers.Abstractions;
using Bing.Admin.EventHandlers.Abstractions.Systems;
using Bing.Admin.EventHandlers.Implements;
using Bing.Admin.EventHandlers.Implements.Systems;
using Bing.Admin.Infrastructure.Cap;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Events.Cap;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// CAP模块
    /// </summary>
    [Description("CAP模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class CapModule : AspNetCoreBingModule
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
            services.AddSingleton<IConsumerServiceSelector, CapConsumerServiceSelector>();
            LoadEvent(services);
            // 添加事件总线服务
            services.AddCapEventBus(o =>
            {
                o.UseEntityFramework<AdminUnitOfWork>();
                o.UseDashboard();
                // 设置处理成功的数据在数据库中保存的时间（秒），为保证系统性能，数据会定期清理
                o.SucceedMessageExpiredAfter = 24 * 3600;
                // 设置失败重试次数
                o.FailedRetryCount = 5;
                o.Version = "admin";
                o.UseRabbitMQ(x =>
                {
                    x.UserName = "admin";
                    x.Password = "bing2019.00";
                    x.HostName = "10.186.132.60";
                });
            });
            return services;
        }

        /// <summary>
        /// 加载事件
        /// </summary>
        /// <param name="services">服务集合</param>
        protected void LoadEvent(IServiceCollection services)
        {
            services.AddTransient<IUserLoginLogMessageEventHandler, UserLoginLogMessageEventHandler>();
            services.AddTransient<ITestMessageEventHandler, TestMessageEventHandler>();
        }
    }
}
