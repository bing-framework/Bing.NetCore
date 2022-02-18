using System;
using System.ComponentModel;
using Bing.Admin.Data;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Admin.EventHandlers.Abstractions;
using Bing.Admin.EventHandlers.Abstractions.Systems;
using Bing.Admin.EventHandlers.Implements;
using Bing.Admin.EventHandlers.Implements.Systems;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Events.Cap;
using Bing.Tracing;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            // 替换CAP消费者服务选择器
            services.AddSingleton<IConsumerServiceSelector, CapConsumerServiceSelector>();
            // 替换CAP订阅执行者
            services.AddSingleton<ISubscribeInvoker, CapSubscribeInvoker>();
            LoadEvent(services);
            // 添加事件总线服务
            services.AddCapEventBus(o =>
            {
                o.UseEntityFramework<AdminUnitOfWork>();
                //o.UseMySql(connection);
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

    /// <summary>
    /// Cap 订阅执行器
    /// </summary>
    public class CapSubscribeInvoker : SubscribeInvoker
    {
        /// <summary>
        /// 日志组件
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// 初始化一个<see cref="CapSubscribeInvoker"/>类型的实例
        /// </summary>
        public CapSubscribeInvoker(ILoggerFactory loggerFactory, IServiceProvider serviceProvider, ISerializer serializer)
            : base(loggerFactory, serviceProvider, serializer)
        {
            _logger = loggerFactory.CreateLogger<CapSubscribeInvoker>();
        }

        /// <summary>
        /// 获取实例
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        /// <param name="context">订阅上下文</param>
        protected override object GetInstance(IServiceProvider provider, ConsumerContext context)
        {
            var capHeader = new CapHeader(context.DeliverMessage.Headers);
            InitTraceIdContext(capHeader);
            return base.GetInstance(provider, context);
        }

        /// <summary>
        /// 初始化跟踪标识上下文
        /// </summary>
        private void InitTraceIdContext(CapHeader capHeader)
        {
            if (capHeader == null)
                return;
            if (!capHeader.TryGetValue("bing-trace-id", out var traceId))
                return;
            _logger.LogDebug("Init TraceId: {0}", traceId);
            if (TraceIdContext.Current == null)
                TraceIdContext.Current = new TraceIdContext(traceId);
            else
                TraceIdContext.Current.TraceId = traceId;
        }
    }
}
