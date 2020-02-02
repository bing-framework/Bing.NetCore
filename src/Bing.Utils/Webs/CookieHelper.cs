using Bing.Helpers;
using Bing.Utils.Helpers;
using Microsoft.AspNetCore.Http;

namespace Bing.Utils.Webs
{
    /// <summary>
    /// Cookie 操作辅助类
    /// </summary>
    public static class CookieHelper
    {
        /// <summary>
        /// 获取 Cookie 值
        /// </summary>
        /// <param name="name">名称</param>
        public static string GetCookie(string name) => GetCookie(Web.HttpContext, name);

        /// <summary>
        /// 获取 Cookie 值
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <param name="name">名称</param>
        public static string GetCookie(HttpContext context, string name) => context?.Request?.Cookies?[name] != null
            ? context.Request.Cookies[name]
            : string.Empty;

        /// <summary>
        /// 写入 Cookie 值。未设置过期时间，则写的是浏览器进程Cookie，一旦浏览器(是浏览器，而非标签页)关闭，则Cookie自动失效
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public static void WriteCookie(string name, string value) =>
            WriteCookie(Web.HttpContext, name, value);

        /// <summary>
        /// 写入 Cookie 值。未设置过期时间，则写的是浏览器进程Cookie，一旦浏览器(是浏览器，而非标签页)关闭，则Cookie自动失效
        /// </summary>
        /// <param name="context">Http上下文</param>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public static void WriteCookie(HttpContext context, string name, string value)
        {
            var cookieOptions = new CookieOptions() { HttpOnly = true };
            context?.Response?.Cookies?.Append(name, value, cookieOptions);
        }

        /// <summary>
        /// 清空 Cookie
        /// </summary>
        public static void ClearCookie() => ClearCookie(Web.HttpContext);

        /// <summary>
        /// 清空 Cookie
        /// </summary>
        /// <param name="context">Http上下文</param>
        public static void ClearCookie(HttpContext context)
        {
            foreach (var cookie in context.Request.Cookies.Keys)
                context.Response.Cookies.Delete(cookie);
        }
    }
}
