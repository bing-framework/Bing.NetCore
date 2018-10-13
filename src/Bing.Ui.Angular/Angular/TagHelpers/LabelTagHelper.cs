using Bing.Ui.Angular.Enums;
using Bing.Ui.Angular.Renders;
using Bing.Ui.Configs;
using Bing.Ui.Renders;
using Bing.Ui.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bing.Ui.Angular.TagHelpers
{
    /// <summary>
    /// 标签
    /// </summary>
    [HtmlTargetElement("bg-label",TagStructure = TagStructure.WithoutEndTag)]
    public class LabelTagHelper:AngularTagHelperBase
    {
        /// <summary>
        /// 文本
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 绑定文本
        /// </summary>
        public string BindText { get; set; }

        /// <summary>
        /// 属性表达式
        /// </summary>
        public ModelExpression For { get; set; }

        /// <summary>
        /// 标签类型
        /// </summary>
        public LabelType Type { get; set; }

        /// <summary>
        /// 日期格式化字符串，格式说明：
        /// 1. 年 - yyyy
        /// 2. 月 - MM
        /// 3. 日 - dd
        /// 4. 时 - HH
        /// 5. 分 - mm
        /// 6. 秒 - ss
        /// 7. 毫秒 - SSS
        /// </summary>
        public string DateFormat { get; set; }

        /// <summary>
        /// 获取渲染器
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        protected override IRender GetRender(Context context)
        {
            return new LabelRender(new Config(context));
        }
    }
}
