using Bing.Ui.Builders;

namespace Bing.Ui.Zorro.Forms.Builders
{
    /// <summary>
    /// NgZorro复选框生成器
    /// </summary>
    public class CheckBoxBuilder : TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="CheckBoxBuilder"/>类型的实例
        /// </summary>
        public CheckBoxBuilder() : base("label")
        {
            base.AddAttribute("nz-checkbox");
        }
    }
}
