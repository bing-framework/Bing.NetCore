using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bing.Webs.Middlewares
{
    /// <summary>
    /// 定义AspNetCore中间件
    /// </summary>
    public interface IMiddleware
    {
        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <returns></returns>
        Task Invoke(HttpContext context);
    }
}
