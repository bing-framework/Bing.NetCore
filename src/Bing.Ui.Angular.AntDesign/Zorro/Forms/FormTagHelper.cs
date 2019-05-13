using Bing.Ui.Angular.Base;
using Bing.Ui.Configs;
using Bing.Ui.Renders;
using Bing.Ui.TagHelpers;
using Bing.Ui.Zorro.Forms.Renders;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bing.Ui.Zorro.Forms
{
    /// <summary>
    /// 表单
    /// </summary>
    [HtmlTargetElement("bg-form")]
    public class FormTagHelper : AngularTagHelperBase
    {
        /// <summary>
        /// 提交事件处理函数，范例：handle()
        /// </summary>
        public string OnSubmit { get; set; }

        /// <summary>
        /// 获取渲染器
        /// </summary>
        /// <param name="context">上下文</param>
        protected override IRender GetRender(Context context)
        {
            return new FormRender(new Config(context));
        }
    }
}
