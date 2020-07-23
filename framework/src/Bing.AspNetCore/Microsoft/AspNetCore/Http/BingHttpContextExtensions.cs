using System.Linq;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// Http上下文(<see cref="HttpContext"/>) 扩展
    /// </summary>
    public static partial class BingHttpContextExtensions
    {
        /// <summary>
        /// 获取远程IP地址
        /// </summary>
        /// <param name="context">Http上下文</param>
        public static string GetRemoteIpAddress(this HttpContext context)
        {
            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ip))
                ip = context.Connection.RemoteIpAddress?.ToString();
            return ip;
        }
    }
}
