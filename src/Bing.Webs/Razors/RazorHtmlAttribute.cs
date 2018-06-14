using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Webs.Razors
{
    /// <summary>
    /// Razor生成Html静态文件
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method,AllowMultiple = false, Inherited = false)]
    public class RazorHtmlAttribute:Attribute
    {
        /// <summary>
        /// 生成路径，相对根路径，范例：/Typings/app/app.component.html
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 路径模板，范例：Typings/app/{area}/{controller}/{controller}-{action}.component.html
        /// </summary>
        public string Template { get; set; }
    }
}
