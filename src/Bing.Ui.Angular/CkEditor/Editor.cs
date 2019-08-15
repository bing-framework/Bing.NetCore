using Bing.Ui.CkEditor.Renders;
using Bing.Ui.Components;
using Bing.Ui.Renders;

namespace Bing.Ui.CkEditor
{
    /// <summary>
    /// 富文本框编辑器
    /// </summary>
    public class Editor : ComponentBase, IEditor
    {
        /// <summary>
        /// 获取渲染器
        /// </summary>
        protected override IRender GetRender()
        {
            return new EditorRender(OptionConfig);
        }
    }
}
