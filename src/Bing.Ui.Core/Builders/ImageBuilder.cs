using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bing.Ui.Builders
{
    /// <summary>
    /// 图片生成器
    /// </summary>
    public class ImageBuilder : TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="ImageBuilder"/>类型的实例
        /// </summary>
        public ImageBuilder() : base("img", TagRenderMode.SelfClosing)
        {
        }
    }
}
