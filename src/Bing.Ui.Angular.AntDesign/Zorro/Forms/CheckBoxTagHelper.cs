using Bing.Ui.Angular.Base;
using Bing.Ui.Configs;
using Bing.Ui.Renders;
using Bing.Ui.TagHelpers;
using Bing.Ui.Zorro.Forms.Renders;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bing.Ui.Zorro.Forms
{
    /// <summary>
    /// 复选框
    /// </summary>
    [HtmlTargetElement("bg-checkbox")]
    public class CheckBoxTagHelper : AngularTagHelperBase
    {
        /// <summary>
        /// 属性表达式
        /// </summary>
        public ModelExpression For { get; set; }

        /// <summary>
        /// [(ngModel)],模型绑定
        /// </summary>
        public string NgModel { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string BindName { get; set; }

        /// <summary>
        /// 标签文本
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// 标签文本
        /// </summary>
        public string BindLabel { get; set; }

        /// <summary>
        /// nzDisabled,禁用
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// [nzDisabled],禁用
        /// </summary>
        public string BindDisabled { get; set; }

        /// <summary>
        /// [nzIndeterminate],是否中间状态
        /// </summary>
        public string Indeterminate { get; set; }

        /// <summary>
        /// (nzOnChange),变更事件处理函数
        /// </summary>
        public string OnChange { get; set; }

        /// <summary>
        /// 获取渲染器
        /// </summary>
        /// <param name="context">上下文</param>
        protected override IRender GetRender(Context context)
        {
            return new CheckBoxRender(new Config(context));
        }
    }
}
