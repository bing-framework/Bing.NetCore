using System;
using System.ComponentModel;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using SkyApm.Diagnostics.CAP;
using SkyApm.Diagnostics.Sql;
using SkyApm.Utilities.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// SkyAPM模块
    /// </summary>
    [Description("SkyAPM模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class SkyApmModule : AspNetCoreBingModule
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
            services
                .AddSkyApmExtensions()
                .AddCap()
                .AddSqlQuery();
            return services;
        }

        private class SqlQueryObserver<T> : IObserver<T>
        {
            private readonly Action<T> _next;

            public SqlQueryObserver(Action<T> next)
            {
                _next = next;
            }

            /// <summary>
            /// 完成
            /// </summary>
            public void OnCompleted()
            {
            }

            /// <summary>
            /// 出错
            /// </summary>
            public void OnError(Exception error)
            {
            }

            /// <summary>
            /// 下一步
            /// </summary>
            public void OnNext(T value)
            {
            }
        }
    }

}
