using System;
using AspectCore.DependencyInjection;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Samples.Hangfire.Jobs;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Samples.Hangfire.Modules
{
    /// <summary>
    /// Hangfire 后台任务模块
    /// </summary>
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class HangfireModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Framework;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 0;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddHangfire(o =>
            {
                o.UseMemoryStorage();
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
            GlobalConfiguration.Configuration.UseActivator(new HangfireDIActivator(app.ApplicationServices));
            app.UseHangfireDashboard();
            app.UseHangfireServer();
            RecurringJob.AddOrUpdate<IDebugLogJob>(x => x.WriteLog(), "0/5 * * * * ? ", TimeZoneInfo.Local);
        }
    }

    /// <summary>
    /// Hangfire DI 激活器
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal class HangfireDIActivator : JobActivator
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 服务解析器
        /// </summary>
        private readonly IServiceResolver _serviceResolver;

        /// <summary>
        /// 初始化一个<see cref="HangfireDIActivator"/>类型的实例
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public HangfireDIActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            if (serviceProvider is IServiceResolver serviceResolver)
                _serviceResolver = serviceResolver;
        }

        /// <summary>
        /// 激活任务
        /// </summary>
        /// <param name="jobType">任务类型</param>
        public override object ActivateJob(Type jobType) => _serviceResolver == null
            ? _serviceProvider.GetService(jobType)
            : _serviceResolver.Resolve(jobType);

        /// <summary>
        /// 开始作用域
        /// </summary>
        /// <param name="context">上下文</param>
        public override JobActivatorScope BeginScope(JobActivatorContext context)
        {
            if (_serviceResolver == null)
                return new MsdiScope(_serviceProvider.CreateScope());
            return new AspectCoreScope(_serviceResolver.CreateScope());
        }

        /// <summary>
        /// AspectCore 作用域
        /// </summary>
        class AspectCoreScope : JobActivatorScope
        {
            /// <summary>
            /// 服务解析器
            /// </summary>
            private readonly IServiceResolver _resolver;

            /// <summary>
            /// 初始化一个<see cref="AspectCoreScope"/>类型的实例
            /// </summary>
            /// <param name="resolver">服务解析器</param>
            public AspectCoreScope(IServiceResolver resolver) => _resolver = resolver;

            /// <summary>
            /// 解析
            /// </summary>
            /// <param name="type">类型</param>
            public override object Resolve(Type type) => _resolver.Resolve(type);

            /// <summary>
            /// 释放作用域
            /// </summary>
            public override void DisposeScope() => _resolver.Dispose();
        }

        /// <summary>
        /// MSDI 作用域
        /// </summary>
        class MsdiScope : JobActivatorScope
        {
            /// <summary>
            /// 作用域
            /// </summary>
            private readonly IServiceScope _scope;

            /// <summary>
            /// 初始化一个<see cref="MsdiScope"/>类型的实例
            /// </summary>
            /// <param name="scope">作用域</param>
            public MsdiScope(IServiceScope scope) => _scope = scope;

            /// <summary>
            /// 解析
            /// </summary>
            /// <param name="type">类型</param>
            public override object Resolve(Type type) => _scope?.ServiceProvider?.GetService(type);

            /// <summary>
            /// 释放作用域
            /// </summary>
            public override void DisposeScope() => _scope?.Dispose();
        }
    }
}
