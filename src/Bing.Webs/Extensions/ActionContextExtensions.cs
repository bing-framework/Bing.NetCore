using Bing.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Webs.Extensions
{
    /// <summary>
    /// 操作上下文(<see cref="ActionContext"/>) 扩展
    /// </summary>
    public static class ActionContextExtensions
    {
        /// <summary>
        /// 获取Area名称
        /// </summary>
        /// <param name="context">操作上下文</param>
        /// <returns></returns>
        public static string GetAreaName(this ActionContext context)
        {
            string area = null;
            if (context.RouteData.Values.TryGetValue("area", out object value))
            {
                area = value.SafeString();
                if (area.IsEmpty())
                {
                    area = null;
                }
            }

            return area;
        }

        /// <summary>
        /// 获取Controller名称
        /// </summary>
        /// <param name="context">操作上下文</param>
        /// <returns></returns>
        public static string GetControllerName(this ActionContext context) => context.RouteData.Values["controller"].ToString();

        /// <summary>
        /// 获取Action名称
        /// </summary>
        /// <param name="context">操作上下文</param>
        /// <returns></returns>
        public static string GetActionName(this ActionContext context)=> context.RouteData.Values["action"].ToString();
    }
}
