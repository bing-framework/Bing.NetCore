using Bing.Ui.Angular.Forms.Configs;
using Bing.Ui.Configs;
using Bing.Ui.Enums;
using Bing.Ui.Renders;
using Bing.Ui.TagHelpers;
using Bing.Ui.Zorro.Forms.Base;
using Bing.Ui.Zorro.Forms.Renders;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bing.Ui.Zorro.Forms
{
    /// <summary>
    /// 文本框
    /// </summary>
    [HtmlTargetElement("bg-textbox")]
    public class TextBoxTagHelper : FormControlTagHelperBase
    {
        /// <summary>
        /// 是否显示清除按钮
        /// </summary>
        public bool ShowClearButton { get; set; }

        /// <summary>
        /// 只读
        /// </summary>
        public bool Readonly { get; set; }

        /// <summary>
        /// 文本框类型
        /// </summary>
        public TextBoxType Type { get; set; }

        /// <summary>
        /// 电子邮件错误消息
        /// </summary>
        public string EmailMessage { get; set; }

        /// <summary>
        /// 最小长度
        /// </summary>
        public string MinLength { get; set; }

        /// <summary>
        /// 最小长度错误消息
        /// </summary>
        public string MinLengthMessage { get; set; }

        /// <summary>
        /// 最大长度
        /// </summary>
        public string MaxLength { get; set; }

        /// <summary>
        /// 最小值
        /// </summary>
        public string Min { get; set; }

        /// <summary>
        /// 最小值错误消息
        /// </summary>
        public string MinMessage { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public string Max { get; set; }

        /// <summary>
        /// 最大值错误消息
        /// </summary>
        public string MaxMessage { get; set; }

        /// <summary>
        /// 正则表达式
        /// </summary>
        public string Regex { get; set; }

        /// <summary>
        /// 正则表达式错误消息
        /// </summary>
        public string RegexMessage { get; set; }

        /// <summary>
        /// 获取渲染器
        /// </summary>
        /// <param name="context">上下文</param>
        protected override IRender GetRender(Context context)
        {
            var config = new TextBoxConfig(context);
            if (config.GetValue<TextBoxType?>(UiConst.Type) == TextBoxType.Multiple)
                config.IsTextArea = true;
            return new TextBoxRender(config);
        }
    }
}
