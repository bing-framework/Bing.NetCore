using Bing.Ui.Angular.Base;
using Bing.Ui.Configs;
using Bing.Ui.Renders;
using Bing.Ui.TagHelpers;
using Bing.Ui.Zorro.Buttons.Renders;
using Bing.Ui.Zorro.Enums;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bing.Ui.Zorro.Buttons
{
    /// <summary>
    /// 按钮
    /// </summary>
    [HtmlTargetElement("bg-button")]
    public class ButtonTagHelper : AngularTagHelperBase
    {
        /// <summary>
        /// text,文本
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// [text],文本属性绑定
        /// </summary>
        public string BindText { get; set; }

        /// <summary>
        /// 颜色
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// 是否验证表单
        /// </summary>
        public bool ValidateForm { get; set; }

        /// <summary>
        /// 禁用
        /// </summary>
        public string Disabled { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public AntDesignIcon Icon { get; set; }

        /// <summary>
        /// 提示
        /// </summary>
        public string Tooltip { get; set; }

        /// <summary>
        /// nzSize,按钮尺寸
        /// </summary>
        public ButtonSize Size { get; set; }

        /// <summary>
        /// nzShape,按钮形状
        /// </summary>
        public ButtonShape Shape { get; set; }

        /// <summary>
        /// [nzLoading],设置加载状态
        /// </summary>
        public string Loading { get; set; }

        /// <summary>
        /// [nzBlock],将按钮宽度调整为其父宽度
        /// </summary>
        public bool Block { get; set; }

        /// <summary>
        /// [nzGhost],设置为透明背景
        /// </summary>
        public bool Ghost { get; set; }

        /// <summary>
        /// 单击事件处理函数,范例：handle()
        /// </summary>
        public string OnClick { get; set; }

        /// <summary>
        /// 获取渲染器
        /// </summary>
        /// <param name="context">TagHelper上下文</param>
        protected override IRender GetRender(Context context)
        {
            return new ButtonRender(new Config(context));
        }
    }
}
