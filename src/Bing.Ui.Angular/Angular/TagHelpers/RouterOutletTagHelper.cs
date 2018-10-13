using Bing.Ui.Angular.Renders;
using Bing.Ui.Configs;
using Bing.Ui.Renders;
using Bing.Ui.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bing.Ui.Angular.TagHelpers
{
    /// <summary>
    /// router-outlet路由出口
    /// </summary>
    [HtmlTargetElement("bg-router-outlet")]
    public class RouterOutletTagHelper:AngularTagHelperBase
    {
        /// <summary>
        /// 获取渲染器
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        protected override IRender GetRender(Context context)
        {
            return new RouterOutletRender(new Config(context));
        }
    }
}
