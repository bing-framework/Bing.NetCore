using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore
{
    /// <summary>
    /// 定义AspNetCore中间件。按照约定的方式定义一个AspNetCore中间件，而不是使用<see cref="Microsoft.AspNetCore.Http.IMiddleware"/>接口，因为后者在调用时需要将中间件注册到DI才可以用，麻烦了点
    /// </summary>
    public interface IMiddleware
    {
        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        Task InvokeAsync(HttpContext context);
    }
}
