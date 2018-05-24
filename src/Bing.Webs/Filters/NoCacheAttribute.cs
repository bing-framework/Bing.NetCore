using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bing.Webs.Filters
{
    /// <summary>
    /// 无缓存过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)]
    public sealed class NoCacheAttribute:ActionFilterAttribute
    {
        /// <summary>
        /// 重写OnResultExecuting()方法，取消页面缓存
        /// </summary>
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, max-age=0";
            context.HttpContext.Response.Headers["Pragma"] = "no-cache";
            context.HttpContext.Request.Headers["Expires"] = "-1";

            base.OnResultExecuting(context);
        }
    }
}
