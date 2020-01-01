using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Events.Cap;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Savorboard.CAP.InMemoryMessageQueue;

namespace Bing.Samples.Modules
{
    /// <summary>
    /// Cap 模块
    /// </summary>
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class CapModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            // 注册CAP事件总线服务
            services.AddCapEventBus(o =>
            {
                o.UseEntityFramework<Bing.Samples.Data.UnitOfWorks.SqlServer.SampleUnitOfWork>();
                o.UseDashboard();
                // 设置处理成功的数据在数据库中保存的时间（秒），为保证系统性能，数据会定期清理
                o.SucceedMessageExpiredAfter = 24 * 3600;
                // 设置失败重试次数
                o.FailedRetryCount = 5;
                o.Version = "bing_test";
                // 启用内存队列
                o.UseInMemoryMessageQueue();
                // 启用RabbitMQ
                //o.UseRabbitMQ(x =>
                //{
                //    x.HostName = "";
                //    x.UserName = "admin";
                //    x.Password = "";
                //});
            });
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            Enabled = true;
        }
    }
}
