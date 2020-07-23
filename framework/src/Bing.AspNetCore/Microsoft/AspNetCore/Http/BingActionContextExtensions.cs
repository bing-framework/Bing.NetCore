using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Http
{
    /// <summary>
    /// Action上下文(<see cref="ActionContext"/>) 扩展
    /// </summary>
    public static partial class BingActionContextExtensions
    {
        /// <summary>
        /// 获取远程IP地址
        /// </summary>
        /// <param name="context">Action上下文</param>
        public static string GetRemoteIpAddress(this ActionContext context) => context.HttpContext.GetRemoteIpAddress();
    }
}
