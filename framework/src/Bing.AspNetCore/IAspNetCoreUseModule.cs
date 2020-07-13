using Microsoft.AspNetCore.Builder;

namespace Bing.AspNetCore
{
    /// <summary>
    /// AspNetCore环境下的应用模块服务
    /// </summary>
    public interface IAspNetCoreUseModule
    {
        /// <summary>
        /// 应用模块服务。仅在AspNetCore环境下调用，非AspNetCore环境请求执行 UseModule(IServiceProvider) 功能
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        void UseModule(IApplicationBuilder app);
    }
}
