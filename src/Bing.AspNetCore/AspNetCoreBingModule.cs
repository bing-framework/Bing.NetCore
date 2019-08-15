using Bing.Core.Modularity;
using Microsoft.AspNetCore.Builder;

namespace Bing.AspNetCore
{
    /// <summary>
    /// 基于AspNetCore环境的模块基类
    /// </summary>
    public class AspNetCoreBingModule : BingModule
    {
        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public virtual void UseModule(IApplicationBuilder app) => base.UseModule(app.ApplicationServices);
    }
}
