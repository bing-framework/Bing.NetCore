using System;
using System.Collections.Generic;
using System.Text;
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
    /// 开关
    /// </summary>
    [HtmlTargetElement("bg-switch")]
    public class SwitchTagHelper : AngularTagHelperBase
    {
        /// <summary>
        /// 属性表达式
        /// </summary>
        public ModelExpression For { get; set; }

        /// <summary>
        /// name,控件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// [name],控件名称
        /// </summary>
        public string BindName { get; set; }

        /// <summary>
        /// nzDisabled,禁用
        /// </summary>
        public bool Disabled { get; set; }

        /// <summary>
        /// [nzDisabled],禁用
        /// </summary>
        public string BindDisabled { get; set; }

        /// <summary>
        /// [(ngModel)],模型绑定
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// (ngModelChange),变更事件处理函数
        /// </summary>
        public string OnChange { get; set; }

        /// <summary>
        /// 获取渲染器
        /// </summary>
        /// <param name="context">上下文</param>
        protected override IRender GetRender(Context context)
        {
            return new SwitchRender(new Config(context));
        }
    }
}
