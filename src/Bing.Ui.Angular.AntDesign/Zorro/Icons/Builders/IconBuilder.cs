using Bing.Ui.Builders;

namespace Bing.Ui.Zorro.Icons.Builders
{
    /// <summary>
    /// 图标生成器
    /// </summary>
    public class IconBuilder : TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="IconBuilder"/>类型的实例
        /// </summary>
        public IconBuilder() : base("i")
        {
            AddAttribute("nz-icon");
        }

        /// <summary>
        /// 添加图标类型
        /// </summary>
        /// <param name="value">图标类型</param>
        public void AddType(string value)
        {
            AddAttribute("nzType", value);
        }

        /// <summary>
        /// 添加图标类型
        /// </summary>
        /// <param name="value">图标类型</param>
        public void AddBindType(string value)
        {
            AddAttribute("[nzType]", value);
        }
    }
}
