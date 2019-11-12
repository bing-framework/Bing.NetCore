using Bing.Events.Cap;
using Bing.Modularity;
using Bing.Samples.Data;
using Bing.Samples.Data.UnitOfWorks.SqlServer;
using Bing.Samples.Domain;
using Microsoft.Extensions.DependencyInjection;
using Savorboard.CAP.InMemoryMessageQueue;

namespace Bing.Samples.Service
{
    /// <summary>
    /// Sample 服务模块
    /// </summary>
    [DependsOn(typeof(SamplesDataModule), typeof(SamplesDomainModule))]
    public class SamplesServiceModule : BingModule
    {
        /// <summary>
        /// 配置服务集合
        /// </summary>
        /// <param name="context">配置服务上下文</param>
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // 注册CAP事件总线服务
            context.Services.AddCapEventBus(o =>
            {
                o.UseEntityFramework<SampleUnitOfWork>();
                o.UseDashboard();
                // 设置处理成功的数据在数据库中保存的时间（秒），为保证系统性能，数据会定期清理
                o.SucceedMessageExpiredAfter = 24 * 3600;
                // 设置失败重试次数
                o.FailedRetryCount = 5;
                o.Version = "test";
                o.UseInMemoryMessageQueue();
            });
        }
    }
}
