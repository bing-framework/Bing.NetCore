using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.TextTemplating
{
    /// <summary>
    /// 模板渲染器
    /// </summary>
    public interface ITemplateRenderer
    {
        /// <summary>
        /// 渲染
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <param name="model">模型</param>
        /// <param name="cultureName">区域性名称</param>
        /// <param name="globalContext">全局上下文</param>
        Task<string> RenderAsync(string templateName, object model = null, string cultureName = null, Dictionary<string, object> globalContext = null);
    }
}
