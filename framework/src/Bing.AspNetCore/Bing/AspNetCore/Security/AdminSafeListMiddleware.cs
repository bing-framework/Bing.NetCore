using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bing.Logs;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.Security
{
    /// <summary>
    /// 安全管理列表中间件
    /// </summary>
    public class AdminSafeListMiddleware : IMiddleware
    {
        /// <summary>
        /// 方法
        /// </summary>
        private readonly RequestDelegate _next;

        /// <summary>
        /// IP白名单
        /// </summary>
        private readonly string _whitelist;

        /// <summary>
        /// 初始化一个<see cref="AdminSafeListMiddleware"/>类型的实例
        /// </summary>
        /// <param name="next">方法</param>
        /// <param name="whitelist">IP白名单</param>
        public AdminSafeListMiddleware(RequestDelegate next, string whitelist)
        {
            _next = next;
            _whitelist = whitelist;
        }

        /// <summary>
        /// 执行中间件拦截逻辑
        /// </summary>
        /// <param name="context">Http上下文</param>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Method != "GET")
            {
                var remoteIp = context.Connection.RemoteIpAddress;
                var log = Log.GetLog(this);
                log.Debug($"来自远程IP地址的请求：{remoteIp}");

                var ips = _whitelist.Split(';');
                var bytes = remoteIp.GetAddressBytes();
                var badIp = true;
                foreach (var ip in ips)
                {
                    var testIp = IPAddress.Parse(ip);
                    if (testIp.GetAddressBytes().SequenceEqual(bytes))
                    {
                        badIp = false;
                        break;
                    }
                }

                if (badIp)
                {
                    log.Info($"来自远程IP地址的禁止请求：{remoteIp}");
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }
            }

            await _next.Invoke(context);
        }
    }
}
