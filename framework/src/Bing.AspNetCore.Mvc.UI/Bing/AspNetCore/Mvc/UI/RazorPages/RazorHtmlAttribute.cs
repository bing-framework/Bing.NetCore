using System;

namespace Bing.AspNetCore.Mvc.UI.RazorPages
{
    /// <summary>
    /// Razor生成Html静态文件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RazorHtmlAttribute : Attribute
    {
        /// <summary>
        /// 生成路径，相对根路径，范例：/Typings/app/app.component.html
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 路径模板，范例：Typings/app/{area}/{controller}/{controller}-{action}.component.html
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// 视图名称，范例：/Home/Index
        /// </summary>
        public string ViewName { get; set; }

        /// <summary>
        /// 是否部分视图，默认：false
        /// </summary>
        public bool IsPartialView { get; set; } = false;
    }
}
