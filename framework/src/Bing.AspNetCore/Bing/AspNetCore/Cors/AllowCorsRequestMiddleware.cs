using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.Cors;

/// <summary>
/// 跨域中间件
/// </summary>
public class AllowCorsRequestMiddleware : IMiddleware
{
    /// <summary>
    /// 方法
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 初始化一个<see cref="AllowCorsRequestMiddleware"/>类型的实例
    /// </summary>
    /// <param name="next">方法</param>
    public AllowCorsRequestMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    /// <param name="context">Http上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // 若为OPTIONS跨域请求则直接返回，不进入后续管道
        if (context.Request.Method.ToLower() == "options")
        {
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST,GET,OPTIONS,DELETE,PUT");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "x-requested-with,content-type,Authorization");
            await context.Response.WriteAsync("");
        }
        else
        {
            await _next(context);
        }
    }
}
