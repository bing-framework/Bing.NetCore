using System;
using System.ComponentModel;
using Bing.Core.Modularity;
using Bing.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore
{
    /// <summary>
    /// AspNetCore Bing 模块管理器
    /// </summary>
    public class AspNetCoreBingModuleManager : BingModuleManager, IAspNetCoreUseModule
    {
        /// <summary>
        /// 应用模块服务
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override void UseModule(IServiceProvider provider)
        {
            var environment = provider.GetService<IHostingEnvironment>();
            if (environment != null)
                throw new Warning("当前处于AspNetCore环境，请使用UseModule(IApplicationBuilder)进行初始化");
            base.UseModule(provider);
        }

        /// <summary>
        /// 应用模块服务。仅在AspNetCore环境下调用，非AspNetCore环境请求执行 <see cref="UseModule(IServiceProvider)"/> 功能
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public void UseModule(IApplicationBuilder app)
        {
            Log.Info("Bing 框架初始化开始");
            var dtStart = DateTime.Now;
            foreach (var module in LoadedModules)
            {
                if (module is AspNetCoreBingModule aspNetCoreModule)
                    aspNetCoreModule.UseModule(app);
                else
                    module.UseModule(app.ApplicationServices);
            }

            var ts = DateTime.Now.Subtract(dtStart);
            Log.Info($"Bing 框架初始化完成，耗时：{ts:g}");
        }
    }
}
